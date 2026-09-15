// The OpenAPI bearer security scheme — authentication-and-authorisation.md
// WI-9 step 9. One document transformer: it adds the http/bearer scheme to
// the components so Swagger UI's Authorize button accepts a pasted token and
// the document honestly advertises that the API is token-validated. No
// per-operation security requirement is stamped: enforcement is the pipeline's
// business (D1), not the document's, and stamping every operation would
// misdescribe none-mode where no validation exists at all.

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Soarscore.Api.Auth;

public sealed class BearerSecurityScheme : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Paste a bearer JWT — an IdP-issued token, or a mock persona token printed at "
                          + "startup under Soarscore:Auth:Mode=mock.",
        };

        return Task.CompletedTask;
    }
}
