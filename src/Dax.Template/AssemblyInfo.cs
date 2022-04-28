using System;
using System.Runtime.CompilerServices;

#if SIGNED
[assembly: InternalsVisibleTo("Dax.Template.Tests, PublicKey=TODO")]
#else
[assembly: InternalsVisibleTo("Dax.Template.Tests")]
#endif

[assembly: CLSCompliant(true)]