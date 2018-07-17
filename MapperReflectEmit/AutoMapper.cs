using System;
using System.Reflection;
using System.Collections.Generic;
using System.Text;

namespace MapperReflect
{
    public class AutoMapper
    {
        public class IMapperKey {
            Type KlassSrc { get; }
            Type KlassDst { get; }

            int HashCode { get; }

            public IMapperKey(Type klassSrc, Type klassDst)
            {
                KlassSrc = klassSrc;
                KlassDst = klassDst;
                HashCode = (KlassSrc.AssemblyQualifiedName + KlassSrc.FullName + KlassDst.AssemblyQualifiedName + KlassDst.FullName).GetHashCode();
            }

            public override bool Equals(object obj)
            {
                if (obj is IMapperKey)
                {
                    return KlassSrc == ((IMapperKey)obj).KlassSrc
                        && KlassDst == ((IMapperKey)obj).KlassDst;
                }
                return false;
            }

            public override int GetHashCode()
            {
                return HashCode;
            }
        }

        private static Dictionary<IMapperKey, IMapper> mappers = new Dictionary<IMapperKey, IMapper>();

        public static void ClearCache()
        {
            mappers.Clear();
        }

        public static IMapper<TSrc, TDest> Build<TSrc, TDest>()
        {
            IMapperKey iMapperKey = new IMapperKey(typeof(TSrc), typeof(TDest));
            IMapper mapper;
            if (!mappers.TryGetValue(iMapperKey, out mapper))
            {
                mapper = new Mapper<TSrc, TDest>();
                mappers.Add(iMapperKey, mapper);
            }
            //As the cache is private we are certain that all the objects are of type (IMapper<TSrc, TDest>)
            return (IMapper<TSrc, TDest>)mapper;
        }

        public static IMapper Build(Type klassSrc, Type klassDst)
        {
            return (IMapper)typeof(AutoMapper)
                .GetMethod("Build", Type.EmptyTypes)
                .MakeGenericMethod(klassSrc, klassDst)
                .Invoke(null, new object[0]);
        }

    }
}
