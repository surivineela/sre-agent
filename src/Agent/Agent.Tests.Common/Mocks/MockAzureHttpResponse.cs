// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using Azure;
using Azure.Core;

namespace Agent.Tests.Common.Mocks
{
    internal class MockAzureHttpResponse<T> : Response<T>
    {
        private readonly Response _response;

        public MockAzureHttpResponse(T value, Response response)
        {
            _response = response;
            Value = value;
        }

        public override T Value { get; }

        public override Response GetRawResponse() => _response;
    }

    internal class MockAzureHttpResponse : Response
    {
        private readonly IReadOnlyDictionary<string, string> _headers;

        public MockAzureHttpResponse(int status)
        {
            Status = status;
            _headers = new Dictionary<string, string>();
        }
        public MockAzureHttpResponse(int status, IDictionary<string, string> headers)
        {
            Status = status;
            _headers = headers.AsReadOnly();
        }

        public override int Status { get; }

        public override string ReasonPhrase => Status.ToString();

        public override Stream? ContentStream { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override string ClientRequestId { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override void Dispose()
        {
        }

        protected override bool ContainsHeader(string name)
        {
            return _headers.ContainsKey(name);
        }

        protected override IEnumerable<HttpHeader> EnumerateHeaders()
        {
            return _headers.Select(x => new HttpHeader(x.Key, x.Value));
        }

        protected override bool TryGetHeader(string name, [NotNullWhen(true)] out string? value)
        {
            return _headers.TryGetValue(name, out value);
        }

        protected override bool TryGetHeaderValues(string name, [NotNullWhen(true)] out IEnumerable<string>? values)
        {
            if (_headers.TryGetValue(name, out string? value))
            {
                values = new[] { value };
                return true;
            }

            values = null;
            return false;
        }
    }
}
