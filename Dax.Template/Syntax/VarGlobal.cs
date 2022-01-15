namespace Dax.Template.Syntax
{
    public class VarGlobal : Var, IGlobalScope
    {
        public VarGlobal() { Scope = VarScope.Global; }
        public bool IsConfigurable { get; set; } = false;
    }
}
