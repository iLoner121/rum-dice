using RumDice.Framework.Datatype;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RumDice.Framework {
    /// <summary>
    /// 一种标准的储存格式（你当然可以自己定义各种各样的格式）
    /// </summary>
    [MyStruct]
    public class StringMap : AnyMap<string>{
        public StringMap(string remark = null) {
            remark = remark ?? string.Empty;
        }
    }
}
