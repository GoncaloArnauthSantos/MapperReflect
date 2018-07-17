using System;
using System.Reflection;
using System.Collections.Generic;
using System.Text;

namespace MapperReflect
{
    public class Mapping
    {
        Type AttributeType { get; }
        bool IsToMapProperties { get; }
        bool IsToMapFields { get; }

        public Mapping(Type attributeType)
            :this(true, true, attributeType)
        {
        }
        
        private Mapping(bool isToMapProperties, bool isToMapFields)
            :this(isToMapProperties, isToMapFields, null)
        {
        }

        private Mapping(bool isToMapProperties, bool isToMapFields, Type attributeType)
        {
            AttributeType = attributeType;
            IsToMapProperties = isToMapProperties;
            IsToMapFields = isToMapFields;
        }

        public static readonly Mapping Fields = new Mapping(false, true);
        public static readonly Mapping Properties = new Mapping(true, false);

        internal virtual IMemberMapping CreateFor(Type klassSrc, Type klassDst)
        {
            if (klassDst.GetTypeInfo().IsValueType || klassDst.GetConstructor(Type.EmptyTypes) != null)
            {
                return MemberToMemberMapping.Create(klassSrc, klassDst, IsToMapProperties, IsToMapFields, AttributeType);
            }
            else
            {
                return MemberToConstructorMapping.Create(klassSrc, klassDst, IsToMapProperties, IsToMapFields, AttributeType);
            }
        }
    }
}
