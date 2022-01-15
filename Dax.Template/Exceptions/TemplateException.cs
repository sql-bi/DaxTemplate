using System;

namespace Dax.Template.Exceptions
{
    public class TemplateException : Exception
    {
        public TemplateException(string message) : base( message ) { }
    }

}
