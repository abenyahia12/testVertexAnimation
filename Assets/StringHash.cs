using System;

//TODO: Disable IL2CPP null checks and alike.. applies to SubString as well #performance
public class StringHash
{
    struct Slot
    {
        public SubString str;
        public uint hash;
        public int nextSlotIndex;
    }

    int[] m_Buckets;
    Slot[] m_Slots;
    int m_NextFreeSlot;

    static int PrimeLookupTable(int index)
    {
#if MAP_DEBUG
        if (index < 0 || index >= 29)
        {
            throw new IndexOutOfRangeException();
        }
#endif
        switch ((uint)index % 29)
        {
            case 0:
                return 5;
            case 1:
                return 13;
            case 2:
                return 23;
            case 3:
                return 47;
            case 4:
                return 97;
            case 5:
                return 193;
            case 6:
                return 389;
            case 7:
                return 769;
            case 8:
                return 1543;
            case 9:
                return 3079;
            case 10:
                return 6151;
            case 11:
                return 12289;
            case 12:
                return 24593;
            case 13:
                return 49157;
            case 14:
                return 98317;
            case 15:
                return 196613;
            case 16:
                return 393241;
            case 17:
                return 786433;
            case 18:
                return 1572869;
            case 19:
                return 3145739;
            case 20:
                return 6291469;
            case 21:
                return 12582917;
            case 22:
                return 25165843;
            case 23:
                return 50331653;
            case 24:
                return 100663319;
            case 25:
                return 201326611;
            case 26:
                return 402653189;
            case 27:
                return 805306457;
            case 28:
                return 1610612741;
        }
        return 0;
    }

    static int NextPrime(int n)
    {
        int i = 0;
        int prime = PrimeLookupTable(i);
        while (n > prime)
        {
            i++;
            prime = PrimeLookupTable(i);
        }
        return prime;
    }

    public StringHash(int startCapacity = 16)
    {
#if MAP_DEBUG
        if (startCapacity < 0)
        {
            throw new ArgumentException();
        }
#endif
        if (startCapacity < 16)
        {
            startCapacity = 16;
        }
        m_Slots = new Slot[startCapacity];
        m_Buckets = new int[NextPrime(startCapacity)];
        m_NextFreeSlot = 0;
    }

    public int GetHash(string str)
    {
        return GetHash(new SubString(str, 0, str?.Length ?? 0));
    }

    public int GetHash(SubString str, bool createOne = true)
    {
#if MAP_DEBUG
        if (str.length == 0)
        {
            throw new ArgumentException();
        }
#endif
        uint hash = str.FNV1AHash();
        int bucket = (int)(hash % m_Buckets.Length);
        int slotIndex = m_Buckets[bucket] - 1;

        while (slotIndex >= 0)
        {
            if (m_Slots[slotIndex].hash == hash)
            {
                if (m_Slots[slotIndex].str.Equals(str))
                {
                    return unchecked ((int)m_Slots[slotIndex].hash);
                }
                hash++;
                bucket = (int)(hash % m_Buckets.Length);
                slotIndex = m_Buckets[bucket] - 1;
            }
            else
            {
                slotIndex = m_Slots[slotIndex].nextSlotIndex;
            }
        }

        if (!createOne)
        {
            return -1;
        }

        if (m_NextFreeSlot >= m_Slots.Length)
        {
            Grow();
            bucket = (int)(hash % m_Buckets.Length);
        }

        slotIndex = m_NextFreeSlot;
        m_NextFreeSlot++;

        m_Slots[slotIndex].str = str;
        m_Slots[slotIndex].hash = hash;
        m_Slots[slotIndex].nextSlotIndex = m_Buckets[bucket] - 1;

        m_Buckets[bucket] = slotIndex + 1;

        return unchecked((int)hash);
    }

    void Grow()
    {
        int newSlotsSize = m_Slots.Length * 2 + 1;
        int newBucketsSize = NextPrime(newSlotsSize * 4 / 3);

        var newSlots = new Slot[newSlotsSize];
        var newBuckets = new int[newBucketsSize];
        Array.Copy(m_Slots, 0, newSlots, 0, m_Slots.Length);

        for (var i = 0; i < m_Slots.Length; i++)
        {
            int bucketIndex = (int)(newSlots[i].hash % newBucketsSize);
            newSlots[i].nextSlotIndex = newBuckets[bucketIndex] - 1;
            newBuckets[bucketIndex] = i + 1;
        }

        m_Slots = newSlots;
        m_Buckets = newBuckets;
    }

    public SubString GetString(int paramHash)
    {
        uint hash = unchecked ((uint) paramHash);
        int bucket = (int)(hash % m_Buckets.Length);
        int slotIndex = m_Buckets[bucket] - 1;
        while (slotIndex >= 0)
        {
            if (m_Slots[slotIndex].hash == hash)
            {
                return m_Slots[slotIndex].str;
            }
            slotIndex = m_Slots[slotIndex].nextSlotIndex;
        }
        return default(SubString);
    }
}

public struct SubString : IEquatable<SubString>
{
    public readonly string str;
    public readonly int offset;
    public readonly int length;

    public SubString(string str, int offset, int length)
    {
        str = str ?? "";
#if MAP_DEBUG
        if (offset < 0 || length < 0 || offset + length > str.Length)
        {
            throw new ArgumentException();
        }
#endif
        this.str = str;
        this.offset = offset;
        this.length = length;
    }

    public bool Equals(SubString other)
    {
        if (length != other.length)
        {
            return false;
        }
        return length == 0 || string.CompareOrdinal(str, offset, other.str, other.offset, length) == 0;
    }

    public uint FNV1AHash()
    {
        uint hash = 2166136261;
        for (int i = offset; i < length; i++)
        {
            hash = unchecked((str[i] ^ hash) * 16777619);
        }
        return hash;
    }

    public override int GetHashCode()
    {
        return unchecked((int)FNV1AHash());
    }

    public override string ToString()
    {
        return length > 0 ? str.Substring(offset, length) : "";
    }
}
