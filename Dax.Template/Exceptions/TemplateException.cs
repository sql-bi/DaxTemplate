using System;

namespace Dax.Template.Exceptions
{
    public class TemplateException : Exception
    {
        public TemplateException(string message)
            : base(message)
        {
        }
    }

    public class TemplateUnexpectedException : Exception
    {
        public TemplateUnexpectedException(string message)
            : base(message)
        {
        }
    }

    public class TemplateConfigurationException : TemplateException
    {
        public TemplateConfigurationException(string message)
            : base(message)
        {
        }
    }
}
