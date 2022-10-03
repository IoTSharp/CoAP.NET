// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Codes
{
    using System;


    public abstract class CoapCode : IEquatable<CoapCode>
    {
        protected CoapCode(CodeClass @class, CodeDetail detail)
        {
            this.Class = @class;
            this.Detail = detail;
        }

        public CodeClass Class { get; }

        public CodeDetail Detail { get; }

        public static bool operator ==(CoapCode left, CoapCode right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(CoapCode left, CoapCode right)
        {
            return !Equals(left, right);
        }

        public override string ToString()
        {
            return $"{this.Class}.{this.Detail}";
        }

        public bool Equals(CoapCode other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Equals(this.Class, other.Class) && Equals(this.Detail, other.Detail);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return this.Equals((CoapCode)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(this.Class, this.Detail);
        }
    }
}
