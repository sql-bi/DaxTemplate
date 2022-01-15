using System.Collections.Generic;

namespace Dax.Template.Interfaces
{
    public interface ICustomTableConfig: IScanConfig
    {
        public Dictionary<string, string> DefaultVariables { get; set; }
    }
}
