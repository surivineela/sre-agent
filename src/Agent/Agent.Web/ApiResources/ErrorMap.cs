//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------


namespace Agent.Web.ApiResources;

public class ErrorMap
{
    public string ErrorCode { get; private set; }

    public string MessageTemplate { get; private set; }

    public ErrorMap(string errorCode, string messageTemplate)
    {
        ErrorCode = errorCode;
        MessageTemplate = messageTemplate;
    }

    public ErrorEntity CreateErrorEntity(params object?[] args)
    {
        string text = string.Format(MessageTemplate, args);

        var errorEntity = new ErrorEntity(code: ErrorCode, message: text);

        return errorEntity;
    }

    public override string ToString()
    {
        try
        {
            return string.Format("{0}: {1}", ErrorCode, MessageTemplate);
        }
        catch (Exception ex)
        {
            return string.Format("{0}: error while evaluating the message - {1}", ErrorCode, ex.Message);
        }
    }

    public static ErrorMap InternalServerError { get { return new ErrorMap("InternalServerError", "Internal server error occurred."); } }
    public static ErrorMap Unauthorized { get { return new ErrorMap("Unauthorized", "The user is not authorized to perform action {0}. Please assign correct roles."); } }
    public static ErrorMap InvalidObjectType { get { return new ErrorMap("InvalidObjectType", "The payload contains invalid object type {0}."); } }
    public static ErrorMap ObjectNameMismatch { get { return new ErrorMap("ObjectNameMismatch", "The object name {0} does not match the name in payload {1}."); } }

}