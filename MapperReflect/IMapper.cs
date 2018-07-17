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
    }
}