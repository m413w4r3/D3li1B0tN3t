using D3B.Diagnostic.Contract;
using Google.Protobuf.Reflection;

namespace D3B.Diagnostic.Resolution;

internal sealed record ResolverSchemaPair(
    DiagnosticMessage Schema,
    MessageDescriptor? Readable,
    ReferenceMessage? Reference,
    PriorMapping? Pin,
    ResolverShapeResult? Shape = null);
