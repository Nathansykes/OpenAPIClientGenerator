using System;
using System.Collections.Generic;
using System.Text;

namespace OpenAPIClientGenerator;
internal class OpenAPIDocumentModelClassDefinition
{
    public string SchemaName { get; set; } = null!;
    public string ClassName { get; set; } = null!;
    public string ClassContent { get; set; } = null!;

    public override int GetHashCode()
    {
        return ClassContent.GetHashCode();
    }
}
