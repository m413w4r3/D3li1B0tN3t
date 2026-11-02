using Google.Protobuf.Reflection;

namespace D3B.Protocol.Mapping;

/// <summary>One exchange of a connection, as the readable protocol names it: the request's field and the response's.</summary>
public sealed class AnchorExchange
{
    /// <summary>Gets the field of the client's message the exchange's request is.</summary>
    public FieldDescriptor Request { get; }

    /// <summary>Gets the field of the server's message the exchange's response is.</summary>
    public FieldDescriptor Response { get; }

    /// <summary>Initializes a new instance of the <see cref="AnchorExchange"/> class.</summary>
    /// <param name="request">The field of the client's message the request is.</param>
    /// <param name="response">The field of the server's message the response is.</param>
    public AnchorExchange(FieldDescriptor request, FieldDescriptor response)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(response);

        Request = request;
        Response = response;
    }
}
