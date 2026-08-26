using System;

namespace KBP.URDT.Driver
{
    /// <summary>
    /// Stable runtime handle of a game object (invariant I8): a short id backed by
    /// a stable key (InstanceID by default), never a hierarchy path. Resolve is O(1).
    /// </summary>
    public readonly struct Handle : IEquatable<Handle>
    {
        public static readonly Handle Invalid = new Handle(null);

        private readonly string _value;

        public Handle(string value)
        {
            _value = string.IsNullOrEmpty(value) ? null : value;
        }

        public string Value
        {
            get { return _value; }
        }

        public bool IsValid
        {
            get { return _value != null; }
        }

        public static Handle FromInstanceId(int instanceId)
        {
            return new Handle("h" + instanceId.ToString());
        }

        public bool Equals(Handle other)
        {
            return string.Equals(_value, other._value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is Handle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _value != null ? _value.GetHashCode() : 0;
        }

        public override string ToString()
        {
            return _value ?? "<invalid>";
        }
    }
}
