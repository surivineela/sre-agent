import { Checkbox, DefaultButton, DocumentCard, DocumentCardDetails, DocumentCardType, IPanel, MessageBar, MessageBarType, PrimaryButton, ProgressIndicator, Stack, TextField, mergeStyles } from "@fluentui/react";
import { MutableRefObject, useEffect, useMemo, useState } from "react";
import ReactMarkdown from "react-markdown";
import rehypeRaw from "rehype-raw";
import { getRequestForAlertStream } from "../Services/Request";
import { AlertStreamPostBody } from "../Models/Response";
import { useStream } from "../Hooks/UseStream";

export interface AlertEditorChatProps {
    alertConfig: any;
    panelRef: MutableRefObject<IPanel>;
    scrollRef: MutableRefObject<HTMLDivElement>;
}

const AlertEditorChat = (props: AlertEditorChatProps) => {
    const [incidentId, setIncidentId] = useState<string>("");
    const [showIncidentDiscussionOnly, setShowIncidentDiscussionOnly] = useState<boolean>(false);
    const [validationError, setValidationError] = useState<string>("");

    const updateIncidentId = (e: any, newValue?: string) => {
        if (newValue === undefined || newValue === null) return;
        setIncidentId(newValue);
    }
    const { streamResponses, isPending: isStreamLoading, mutateAsync: startStreamAsync, error: streamError } = useStream<string>();

    //Always scroll to the bottom when there is a new response
    useEffect(() => {
        if (props.scrollRef.current) {
            props.scrollRef.current.scrollIntoView({ behavior: "smooth" });
        }
    }, [streamResponses]);



    const streamAlert = async () => {
        if (!validateInput()) return;

        const postBody: AlertStreamPostBody = {
            source: "editor",
            IncidentId: incidentId,
            customAlertConfig: props.alertConfig,
            agentMode: props.alertConfig["agentMode"] ?? null,
        };
        const req = getRequestForAlertStream(postBody);
        await startStreamAsync(req);
    }

    const validateInput = () => {
        setValidationError("");
        if (!incidentId) {
            setValidationError("Please enter a valid incident ID.");
            return false;
        }
        const num = Number.parseInt(incidentId);
        if (Number.isNaN(num) || num <= 0) {
            setValidationError("Incident ID must be a number.");
            return false;
        }
        return true;
    }

    const displayErrorMessage = useMemo(() => {
        if (validationError) {
            return validationError;
        } else if (streamError) {
            return streamError.message;
        } else {
            return "";
        }
    }, [validationError, streamError]);

    const processMarkdownLinks = (message: string): string => {
        if (!message) return message;

        // Then, process existing <a> tags without target="_blank" attribute
        const anchorTagRegex = /<a\s+(?![^>]*target=["']_blank["'])([^>]*)href=["']([^"']+)["']([^>]*)>([^<]+)<\/a>/g;
        let processedMessage = message.replace(anchorTagRegex, (match, attrsBefore, href, attrsAfter, text) => {
            return `<a ${attrsBefore}href="${href}" ${attrsAfter}target="_blank">${text}</a>`;
        });

        // First, process markdown links: [text](url)
        const markdownLinkRegex = /\[([^\]]+)\]\(([^)]+)\)/g;
        processedMessage = processedMessage.replace(markdownLinkRegex, (match, text, url) => {
            return `<a href="${url}" target="_blank">${text}</a>`;
        });

        return processedMessage;
    }

    const displayStreamResponses = useMemo(() => {
        const prefixes = [
            '[post_icm_discussion_entry]',
            '[transfer_icm_incident]',
            '[mitigate_icm_incident]',
            '[resolve_icm_incident]',
            '[execute_kusto_query_on_cluster]'
        ];

        if (!streamResponses) return [];

        return streamResponses.split("\x00").map(t => {
            const trimmedText = t.trim().replace("\n", "");
            return processMarkdownLinks(trimmedText);
        }).filter(t => {
            if (showIncidentDiscussionOnly) {
                return prefixes.some(prefix => t.startsWith(prefix));
            } else {
                return t !== "";
            }
        });
    }, [streamResponses, showIncidentDiscussionOnly]);

    const cardStyles = mergeStyles({
        maxWidth: "100%",
        padding: "8px",
        height: "auto",
    });



    return (
        <Stack tokens={{ childrenGap: 10 }}>
            <TextField label="Incident Id" onChange={updateIncidentId} disabled={isStreamLoading}/>
            <p>Clicking the 'Send' button initiates a test run only, without impacting the incident or executing any actions.</p>
            <Stack horizontal tokens={{ childrenGap: 20 }} horizontalAlign="start">
                <PrimaryButton text="Send" onClick={(e) => { streamAlert() }} disabled={isStreamLoading}/>
                <DefaultButton text="Cancel" onClick={(e) => props.panelRef.current.dismiss()} />
            </Stack>
            <Stack styles={{ root: { overflowY: "auto" } }} tokens={{ childrenGap: 10 }}>
                {streamResponses?.length > 0 ? <Checkbox label="Show incident discussion only" checked={showIncidentDiscussionOnly} onChange={(e, checked) => setShowIncidentDiscussionOnly(!!checked)} /> : null}
                {displayErrorMessage ? <MessageBar messageBarType={MessageBarType.error} isMultiline>{displayErrorMessage}</MessageBar> :
                    displayStreamResponses.map((response, index) => {
                        return (
                            <DocumentCard type={DocumentCardType.compact} className={cardStyles} key={index}>
                                <DocumentCardDetails>
                                    <ReactMarkdown rehypePlugins={[rehypeRaw]} children={response} />
                                </DocumentCardDetails>
                            </DocumentCard>
                        );
                    })
                }
            </Stack>
            {isStreamLoading ? <ProgressIndicator label="Loading chat" /> : null}
        </Stack>
    );
}

export default AlertEditorChat;