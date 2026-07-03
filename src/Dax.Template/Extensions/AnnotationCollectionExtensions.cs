using Microsoft.AnalysisServices.Tabular;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Dax.Template.Extensions;

internal static class AnnotationCollectionExtensions
{
    /// <summary>
    /// Adds or updates annotations on a TOM metadata-object collection from a name/value source,
    /// replacing the value of any existing annotation matched by name.
    /// </summary>
    public static void UpsertAnnotations<TOwner>(this NamedMetadataObjectCollection<Annotation, TOwner> annotations, IEnumerable<KeyValuePair<string, string>>? source, CancellationToken cancellationToken = default)
        where TOwner : MetadataObject
    {
        if (source is null) return;
        foreach (var annotation in source)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var annotationName = annotation.Key;
            var annotationValue = annotation.Value.ToString();

            Annotation? tabularAnnotation = annotations.FirstOrDefault(a => a.Name == annotationName);
            if (tabularAnnotation is null)
            {
                tabularAnnotation = new Annotation { Name = annotationName, Value = annotationValue };
                annotations.Add(tabularAnnotation);
            }
            else
            {
                tabularAnnotation.Value = annotationValue;
            }
        }
    }
}