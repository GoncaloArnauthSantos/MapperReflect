using System;
using System.Collections.Generic;
using System.Text;

namespace MapperReflect
{
    public interface IMapper
    {
        IMapper Bind(Mapping m);
        object Map(object src);
        object[] Map(object[] src);
        IMapper Match(string nameFrom, string nameDest);
        IMapper For<R>(string nameTo, Func<R> func);
    }

    public interface IMapper<TSrc, TDest> : IMapper
    {
        TDest Map(TSrc src);
        TDest[] Map(TSrc[] src);
        E Map<E>(IEnumerable<TSrc> src) where E : ICollection<TDest>, new();
        IEnumerable<TDest> MapLazy(IEnumerable<TSrc> src);
    }
}