import { mergeStyles, MessageBar, MessageBarType, ProgressIndicator } from "@fluentui/react";
import { QueryStatus } from "@tanstack/react-query";
import React from "react";
import ErrorUtilities from "../Helpers/Error";

interface LoadingErrorWrapperProps extends React.PropsWithChildren {
    error: Error;
    status: QueryStatus;
    renderLoading?: (() => React.ReactNode) | React.ReactNode | (() => string) | string;
    renderError?: ((error: Error) => React.ReactNode) | React.ReactNode | ((error: Error) => string) | string;
}

const contentStyles = mergeStyles({
    fontSize: "15px"
});

const LoadingErrorWrapper = (props: LoadingErrorWrapperProps) => {

    const getErrorComponent = (content: React.ReactNode | string, defaultContent: string) => {
        content = validateContent(content) ? content : defaultContent;
        return (
            <MessageBar messageBarType={MessageBarType.error} isMultiline>
                <div className={contentStyles}>{content}</div>
            </MessageBar>
        );
    }

    const getLoadingComponent = (content: React.ReactNode | string, defaultContent: string) => {
        content = content && validateContent(content) ? content : defaultContent;
        if (typeof content === "string") {
            return <ProgressIndicator label={content} className={contentStyles} />;
        } else {
            return content;
        }
    }

    const validateContent = (content: React.ReactNode | string): boolean => {
        return React.isValidElement(<>{content}</>);
    }



    switch (props.status) {
        case "pending":
            let loadingContent = null;
            const defaultLoadingContent = "Loading...";
            if (!props.renderLoading) {
                loadingContent = defaultLoadingContent;
            } else if (typeof props.renderLoading === "function") {
                loadingContent = props.renderLoading();
            } else {
                loadingContent = props.renderLoading;
            }

            return getLoadingComponent(loadingContent, defaultLoadingContent);
        case "error":
            let errorContent = null;
            const defaultErrorContent = ErrorUtilities.defaultErrorMessage;
            if (!props.renderError) {
                errorContent = defaultErrorContent;
            } else if (typeof props.renderError === "function") {
                errorContent = props.renderError(props.error);
            } else {
                errorContent = props.renderError;
            }
            return getErrorComponent(errorContent, defaultErrorContent);

        case "success":
            return <>{props.children}</>
        default:
            return null;
    }
}

export default LoadingErrorWrapper;