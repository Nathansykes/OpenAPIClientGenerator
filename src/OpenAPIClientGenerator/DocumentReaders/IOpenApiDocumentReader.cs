using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenAPIClientGenerator.DocumentReaders;
internal interface IOpenApiDocumentReader
{
    Task<OpenApiDocument> ReadDocumentAsync();
}
