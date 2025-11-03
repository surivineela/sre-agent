# source .env/bin/activate
import requests
import json
import os
import re
import base64
from time import sleep
from openai import AzureOpenAI
from dotenv import load_dotenv
from azure.identity import DefaultAzureCredential, get_bearer_token_provider

# Load environment variables from .env file
load_dotenv()
OPENAI_ENDPOINT = os.getenv("OPENAI_ENDPOINT")
OPENAI_API_KEY = os.getenv("OPENAI_API_KEY")
OPENAI_EMBEDDING_MODEL = os.getenv("OPENAI_EMBEDDING_MODEL")
OPENAI_VISION_MODEL = os.getenv("OPENAI_VISION_MODEL")
AZURE_SEARCH_ENDPOINT = os.getenv("AZURE_SEARCH_ENDPOINT")
AZURE_SEARCH_API_KEY = os.getenv("AZURE_SEARCH_API_KEY")
GITHUB_ORG = os.getenv("GITHUB_ORG")
GITHUB_REPO_NAME = os.getenv("GITHUB_REPO_NAME")
GITHUB_PAT_TOKEN = os.getenv("GITHUB_PAT_TOKEN")

class CommentsOfGitHubIssueForTagging:
    def __init__(self, commentTimestamp=None, body=None):
        self.commentTimestamp = commentTimestamp
        self.body = body

    def __repr__(self):
        return f"[{self.commentTimestamp}] : {self.body}"

    def __str__(self):
        return repr(self)

    def to_dict(self):
        if(self.body is None or self.body == ""):
            return {}
        else:
            return {
                "commentTimestamp": self.commentTimestamp,
                "body": self.body
            }

def CommentsOfGitHubIssueForTagging_json_encoder(obj):
    if isinstance(obj, CommentsOfGitHubIssueForTagging):
        return obj.to_dict()
    raise TypeError(f"Object of type {type(obj)} is not JSON serializable")

class GitHubIssueForTagging:
    def __init__(self, id=None, issueId=None, issueUrl=None, owner=None, repository=None, title=None, body=None, comments=None, labels=None,
                 state=None, descriptiveSummary=None, createdTimestamp = None, lastUpdatedTimestamp=None, summaryVector=None):
        self.id = str(id)
        self.issueId = str(issueId)
        self.issueUrl = issueUrl
        self.owner = owner
        self.repository = repository
        self.title = title
        self.body = body
        self.comments = comments if comments is not None else []
        self.labels = labels
        self.state = state
        self.descriptiveSummary = descriptiveSummary
        self.createdTimestamp = createdTimestamp
        self.lastUpdatedTimestamp = lastUpdatedTimestamp
        self.summaryVector = None

    def __repr__(self):
        comments_string = '\n'.join(repr(comment) for comment in self.comments)
        return f"\nGitHubIssueForTagging(id={self.id}, issueId={self.issueId}, issueUrl={self.issueUrl}, labels={self.labels},\ntitle={self.title},\ndescription={self.body},\ncomments={comments_string},\ndetailedSummary={self.descriptiveSummary}\nState={self.state}, createdTimestamp={self.createdTimestamp}, lastUpdatedTimestamp={self.lastUpdatedTimestamp}, summaryVectorGenerated={ 'No' if self.summaryVector is None or len(self.summaryVector) < 1 else 'Yes'})\n"

    def __str__(self):
        return repr(self)

    def to_dict(self):
        # Convert the comments list to a JSON string
        comments_json = json.dumps([comment.to_dict() for comment in self.comments if comment.to_dict()]) if self.comments else ""

        return {
            "id": self.id,
            "issueId": self.issueId,
            "issueUrl": self.issueUrl,
            "owner": self.owner,
            "repository": self.repository,
            "title": self.title,
            "body": self.body,
            "comments": comments_json,
            "labels": self.labels,
            "state": self.state,
            "descriptiveSummary": self.descriptiveSummary,
            "createdTimestamp": self.createdTimestamp,
            "lastUpdatedTimestamp": self.lastUpdatedTimestamp,
            "summaryVector": self.summaryVector if self.summaryVector is not None else []
        }

    def init_summaryVector(self):
        self.summaryVector = create_azure_openai_embeddings(self.descriptiveSummary)
        return True

def GitHubIssueForTagging_json_encoder_for_display(obj):
    if isinstance(obj, GitHubIssueForTagging):
        obj_dict = obj.to_dict()
        # Remove the summaryVector field from the dictionary
        if "summaryVector" in obj_dict and obj_dict["summaryVector"] is not None and len(obj_dict["summaryVector"]) > 0:
            del obj_dict["summaryVector"]
        return obj_dict
    raise TypeError(f"Object of type {type(obj)} is not JSON serializable")

def summarize_user_provided_image_if_present(content:str) -> str:
    if content is None or content == "":
        return ""
    # RegEx pattern to match image URLs of the form [Image](https://github.com/user-attachments/assets/GUID) and read the entire URL in a match variable
    pattern = r'\[Image\]\((https://github\.com/user-attachments/assets/[^\)]+)\)'
    # Find all matches in the content. Set the search flag to Global to find all matches
    matches = re.findall(pattern, content, re.MULTILINE | re.IGNORECASE)

    # If no matches are found, return what was passed in
    if not matches:
        return content

    # If matches are found, for each match, fetch the image content in base64 format and store it in a dictionary with image URL as key and base64 string as value
    image_dict = {}
    for match in matches:
        image_dict[f"{match}"] = None

    for key, value in image_dict.items():
        # fetch the image content in base64 format
        response = requests.get(key)
        if response.status_code == 200:
            # Extract the image mime type from the response headers
            content_type = response.headers.get('Content-Type', 'image/jpeg')

            # Encode the image content in base64 format
            image_content_base64 = base64.b64encode(response.content).decode('utf-8')
            # Update the dictionary with the base64 string
            image_dict[key] = image_content_base64

            #ToDo: Extract text from image using OpenAI Vision API
            image_description = call_azure_openai_vision_api(content_type, image_content_base64)

            # Replace the image URL in the content with imageURL \n=========\n base64 string \n============\n
            content = content.replace(f"[Image]({key})", f"[Image]({key})\n=========Image description\n{image_description}\n============\n")

    return content

def get_issue_comments(issue_comments_url, headers) -> list[CommentsOfGitHubIssueForTagging]:
    comments = []
    sleep(1)  # Sleep for 1 second to avoid hitting the rate limit
    response = requests.get(issue_comments_url, headers=headers)
    if response.status_code == 200:
        comments_data = json.loads(response.text)
        for comment in comments_data:
            if comment["body"] is None or comment["body"] == "":
                comment["body"] = ""

            comment_obj = CommentsOfGitHubIssueForTagging(
                commentTimestamp = comment["updated_at"],
                body = summarize_user_provided_image_if_present(comment["body"])
            )
            comments.append(comment_obj)
    else:
        print(f"Error fetching issue comments. StatusCode: {response.status_code}")
    return comments

def get_issue_transferred_timestamp(timeline_url, headers) -> str:
    sleep(1) # Sleep for 1 second to avoid hitting the rate limit
    response = requests.get(timeline_url, headers=headers)
    if response.status_code == 200:
        timeline = json.loads(response.text)
        for event in timeline:
            if event["event"] == "transferred":
                return event["created_at"]
    else:
        print(f"Error fetching issue timeline. StatusCode: {response.status_code}")
        return None

# Dictionary to hold a map of user id to org names they are a part of
user_orgs_map = {}
def get_user_orgs(user_id, headers):
    if user_id.lower() in user_orgs_map:
        return user_orgs_map[user_id.lower()]

    # Fetch the organizations for the user
    sleep(1)  # Sleep for 1 second to avoid hitting the rate limit
    orgs_url = f"https://api.github.com/users/{user_id}/orgs"
    response = requests.get(orgs_url, headers=headers)
    if response.status_code == 200:
        orgs = json.loads(response.text)
        # Store the organization names in the dictionary
        if any(org["login"].lower() in ["azure", "microsoft"] for org in orgs):
            user_orgs_map[user_id.lower()] = [org["login"].lower() for org in orgs]
        else:
            user_orgs_map[user_id.lower()] = ["ExternalUser"]

        return user_orgs_map[user_id.lower()]
    else:
        print(f"Error fetching user organizations. URL:{orgs_url} StatusCode: {response.status_code}")
        return None

def create_azure_openai_embeddings(text: str) -> list[float]:
    sleep(1)  # Sleep for 1 second to avoid hitting the rate limit
    client = AzureOpenAI(
        azure_endpoint=OPENAI_ENDPOINT,
        api_key=OPENAI_API_KEY,
        api_version="2023-05-15",
    )

    response = client.embeddings.create(
        model=OPENAI_EMBEDDING_MODEL,
        input=text
    )
    #print(f"Response from Azure OpenAI Embeddings: {response.data[0].embedding}")
    #input("From create_azure_openai_embeddings --> Press Enter to continue...")
    return response.data[0].embedding

def call_azure_openai_vision_api(imageMimeType:str, base64Image:str) -> str:
    sleep(1)  # Sleep for 1 second to avoid hitting the rate limit
    client = AzureOpenAI(
        azure_endpoint=OPENAI_ENDPOINT,
        api_key=OPENAI_API_KEY,
        api_version="2024-05-01-preview",
    )
    messages = [
        {
            "role": "system",
            "content": "You are an AI assistant that describes images accurately and concisely. Focus on technical details if the image contains code, error messages, or technical content."
        },
        {
            "role": "user",
            "content": [
                {
                    "type": "text",
                    "text": "Please describe what you see in this image. If there's code or error messages, or technical content, include those details."
                },
                {
                    "type": "image_url",
                    "image_url": {
                        "url": f"data:{imageMimeType};base64,{base64Image}"
                    }
                }
            ]
        }
    ]

    # Call the API
    response = client.chat.completions.create(
        model=OPENAI_VISION_MODEL,
        messages=messages,
        max_completion_tokens=1000,
        temperature=0.3
    )

    # Return the description
    return response.choices[0].message.content

def call_azure_openai(systemPrompt, chat_history):
    sleep(1)  # Sleep for 1 second to avoid hitting the rate limit

    # Initialize Azure OpenAI Service client with Entra ID authentication
    # token_provider = get_bearer_token_provider(
    #     DefaultAzureCredential(),
    #     "https://cognitiveservices.azure.com/.default"
    # )

    client = AzureOpenAI(
        azure_endpoint=OPENAI_ENDPOINT,
        api_key=OPENAI_API_KEY,
        api_version="2024-05-01-preview",
    )

    chat_prompt = [
        {
            "role": "system",
            "content": [
                {
                    "type": "text",
                    "text": systemPrompt
                }
            ]
        }
    ]

    messages = chat_prompt

    if chat_history:
        messages.extend(chat_history)

    completion = client.chat.completions.create(
        model=OPENAI_VISION_MODEL,
        messages=messages,
        max_completion_tokens=4096,
        temperature=0.1,
        top_p=0.95,
        frequency_penalty=0,
        presence_penalty=0,
        stop=None,
        stream=False
    )

    #print(completion.to_json())
    #input("From call_azure_openai --> Press Enter to continue...")
    #print(f"\n{json.dumps(completion.choices[0].message.content)}\n\n")
    #input("From call_azure_openai --> Press Enter to continue...")
    return completion.choices[0].message.content

def get_issue_summarization(title, body, comments:list[CommentsOfGitHubIssueForTagging]) -> str:
    #systemPrompt = f"Given the details of a GitHub issue. read the entire issue, including comments thoroughly. Provide a detailed descriptive summary of the issue that captures the essence of the issue and its discussion. Include relevant details, such as the problem being addressed, proposed solutions, error messages, stack traces etc. and any conclusions reached. The summary should be comprehensive and reflect the key points of the issue and its comments. Do not include any personal opinions or interpretations. The summary should be in English.\n\n "
    systemPrompt = '''Summarize the content of a GitHub issue, including the original issue title, its description and entire discussion thread, to create a verbose and descriptive summary. The summary should include key details such as error messages, stack traces, code snippets, clarifications, and resolutions.
# Steps
1. Analyze the original issue description to capture the main problem, including any provided error messages, stack traces and code snippets.
2. Review the discussion thread for:
   - Suggestions, clarifications, and proposed resolutions.
   - Relevant responses highlighting key information or developments.
   - Final conclusions or resolutions, if any.
3. Construct a descriptive summary that encapsulates the issue and discussion highlights, including error messages, stack traces, code snippets, and actions taken.

# Output Format
Output a single message containing detailed verbose summary of GitHub issue and thread details. Include problems, error messages, stack traces, code snippets, suggestions, and resolutions. If the discussion ends with unresolved elements, note them in the summary as appropriate.

# Notes
- Ensure the descriptive summary is verbose, avoiding overly technical language unless necessary (e.g., quoting an error message, stack trace, code snippet etc.). Do not come up with recommendations for next steps on your own.
'''
    commentsStr = "\n\n".join([repr(comment) for comment in comments])
    chat_history = [
        {"role": "user", "content": [{"type": "text", "text": f"Title:\n{title}\n\nProblemDescription:\n{body}\n\nDiscussionThread:\n{commentsStr}"}]}
    ]
    response = call_azure_openai(systemPrompt, chat_history)
    # Parse the response to strip off ```json and ``` from the start and end
    #if response.startswith("```json"):
    #    response = response[8:]
    #if response.endswith("```"):
    #    response = response[:-3]
    # Convert the response to JSON format

    #response = json.loads(response)
    #print(f"Descriptive summary: {response}")
    #input("From get_issue_summarization --> Press Enter to continue...")
    return response

def get_cognitive_index_name(owner, repo) -> str:
    index_name = f"githubissues_{owner}_{repo}".lower()
    return index_name

def delete_cognitive_index(owner, repo) -> bool:
    index_name = get_cognitive_index_name(owner, repo)
    azure_search_url = f"{AZURE_SEARCH_ENDPOINT}/indexes/{index_name}?api-version=2024-07-01"
    azure_headers = {
        'Content-Type': 'application/json',
        'api-key': AZURE_SEARCH_API_KEY
    }

    input("About to delete index. Press any key to continue.. Terminate the program to exit.")
    # Delete the index
    response = requests.delete(azure_search_url, headers=azure_headers)
    if response.status_code > 199 and response.status_code < 300:
        print(f"Index {index_name} deleted successfully.")
        return True
    elif response.status_code == 404:
        print(f"Index {index_name} not found. No action taken.")
        return True
    else:
        print(f"Error deleting index {index_name}. StatusCode: {response.status_code}")
        return False

def create_cognitive_index_if_not_exists(owner, repo) -> bool:
    index_name = get_cognitive_index_name(owner, repo)
    #Azure Cognitive Search endpoint and API ey
    azure_search_url = f"{AZURE_SEARCH_ENDPOINT}/indexes/{index_name}?api-version=2024-07-01"
    azure_headers = {
        'Content-Type': 'application/json',
        'api-key': AZURE_SEARCH_API_KEY
    }

    #Check if the index already exists
    response = requests.get(azure_search_url, headers=azure_headers)
    if response.status_code == 200:
        print(f"Index {index_name} already exists.")
        return True
    elif response.status_code == 404:
        #Create the index
        index_definition = {
            "name": index_name,
            "fields": [
                {"name": "id", "type": "Edm.String", "key": True, "filterable": True, "retrievable": True},
                {"name": "issueId", "type": "Edm.String", "retrievable": True, "filterable": True, "searchable": True},
                {"name": "issueUrl", "type": "Edm.String", "retrievable": True},
                {"name": "owner", "type": "Edm.String", "retrievable": True, "filterable": True, "searchable": True},
                {"name": "repository", "type": "Edm.String", "retrievable": True, "filterable": True, "searchable": True},
                {"name": "title", "type": "Edm.String", "retrievable": True, "searchable": True},
                {"name": "body", "type": "Edm.String", "retrievable": True, "searchable": True},
                {"name": "comments", "type": "Edm.String", "retrievable": True, "searchable": True},
                {"name": "labels", "type": "Edm.String", "retrievable": True, "filterable": True, "searchable": True},
                {"name": "state", "type": "Edm.String", "retrievable": True, "filterable": True, "searchable": True},
                {"name": "descriptiveSummary", "type": "Edm.String", "retrievable": True, "searchable": True},
                {"name": "createdTimestamp", "type": "Edm.String", "retrievable": True},
                {"name": "lastUpdatedTimestamp", "type": "Edm.String", "retrievable": True},
                {"name": "summaryVector", "type": "Collection(Edm.Single)", "searchable": True, "retrievable": True, "filterable": False, "dimensions": 1536, "vectorSearchProfile": f"vector-profile-{index_name}" }
            ],
            "vectorSearch": {
                "algorithms": [
                    {
                        "name": f"vector-config-{index_name}",
                        "kind": "exhaustiveKnn",
                        "exhaustiveKnnParameters": {
                            "metric": "cosine"
                        }
                    }
                ],
                "vectorizers": [
                    {
                        "name": f"vectorizer-{index_name}",
                        "kind": "azureOpenAI",
                        "azureOpenAIParameters": {
                            "resourceUri": OPENAI_ENDPOINT,
                            "deploymentId": OPENAI_EMBEDDING_MODEL,
                            "apiKey": OPENAI_API_KEY,
                            "modelName": OPENAI_EMBEDDING_MODEL
                        }
                    }
                ],
                "profiles": [
                    {
                        "name": f"vector-profile-{index_name}",
                        "algorithm": f"vector-config-{index_name}",
                        "vectorizer": f"vectorizer-{index_name}",
                    }
                ]
            }
        }

        response = requests.put(azure_search_url, headers=azure_headers, data=json.dumps(index_definition))
        if response.status_code == 201:
            print(f"Index {index_name} created successfully.")
            return True
        else:
            print(f"Error creating index {index_name}. StatusCode: {response.status_code} Response: {response.text}")
            return False

def get_issue_from_cognitive_search(owner, repo, id) -> GitHubIssueForTagging:
    index_name = get_cognitive_index_name(owner, repo)
    # Azure Cognitive Search endpoint and API key

    azure_cognitive_headers = {
        'Content-Type': 'application/json',
        'api-key': AZURE_SEARCH_API_KEY
    }

    # Search for the issue in Azure Cognitive Search, apply a filter for id
    search_url = f"{AZURE_SEARCH_ENDPOINT}/indexes/{index_name}/docs?search={id}&$filter=id eq '{id}'&api-version=2024-07-01"
    sleep(0.5) # Sleep for 0.5 seconds to avoid hitting the rate limit
    response = requests.get(search_url, headers=azure_cognitive_headers)
    if response.status_code != 200:
        if response.status_code == 404:
            print(f"Issue with id {id} not found in Azure Cognitive Search.")
        else:
            print(f"Error fetching issue from Azure Cognitive Search. StatusCode: {response.status_code}")
        return None
    search_results = json.loads(response.text)
    if not search_results["value"]:
        #print(f"No issue found with id {id} in Azure Cognitive Search.")
        return None
    else:
        issue_data = search_results["value"][0]

        processedIssueFromCognitive = GitHubIssueForTagging(
            id=issue_data.get("id"),
            issueId=issue_data.get("issueId"),
            issueUrl=issue_data.get("issueUrl"),
            owner=issue_data.get("owner"),
            repository=issue_data.get("repository"),
            title=issue_data.get("title"),
            body=issue_data.get("body"),
            labels=issue_data.get("labels"),
            state=issue_data.get("state"),
            descriptiveSummary=issue_data.get("descriptiveSummary"),
            lastUpdatedTimestamp=issue_data.get("lastUpdatedTimestamp")
        )

        # Process comments if they exist
        comments_data = issue_data.get("comments", None)
        if comments_data is not None and isinstance(comments_data, str) and len(comments_data) > 0:
            try:
                comments_data = json.loads(comments_data)
            except json.JSONDecodeError:
                print(f"Error decoding comments JSON: {comments_data}")
                comments_data = []
            # Create comment objects
            comments = []
            for comment in comments_data:
                if isinstance(comment, dict):
                    comments.append(CommentsOfGitHubIssueForTagging(
                        commentTimestamp=comment.get("commentTimestamp"),
                        body=comment.get("body")
                    ))
            processedIssueFromCognitive.comments = comments
        return processedIssueFromCognitive

def ignore_issue(owner, repo, rawIssue, headers) -> str:
    # Check if the issue is a pull request
    if 'pull_request' in rawIssue:
        print(f"Skipping... Issue {rawIssue['number']} is a pull request.")
        return "PullRequest"

    # user_orgs = get_user_orgs(rawIssue["user"]["login"], headers)
    # if user_orgs:
    #     # Check if the issue creator is part of the Azure or Microsoft organization
    #     if any(org.lower() == "azure" for org in user_orgs):
    #         print(f"Skipping... Issue {rawIssue['number']} is from Azure.")
    #         return "CreatorAzureOrg"

    #     if any(org.lower() == "microsoft" for org in user_orgs):
    #         print(f"Skipping... Issue {rawIssue['number']} is from Microsoft.")
    #         return "CreatorMicrosoftOrg"

    lastUpdatedTime = rawIssue["updated_at"]
    issueFromCognitive = get_issue_from_cognitive_search(owner, repo, rawIssue["id"])
    if issueFromCognitive is not None and issueFromCognitive.lastUpdatedTimestamp == lastUpdatedTime:
        print(f"Skipping... Issue {rawIssue['number']} has not been updated.")
        return "IssueNotUpdated"

    return None

def try_batch_update_cognitive_index(owner, repo, docList) -> bool:
    sleep(1)  # Sleep for 1 second to avoid hitting the rate limit
    index_name = get_cognitive_index_name(owner, repo)
    # Azure Cognitive Search endpoint and API key
    azure_search_url = f"{AZURE_SEARCH_ENDPOINT}/indexes/{index_name}/docs/index?api-version=2024-07-01"
    azure_cognitive_headers = {
        'Content-Type': 'application/json',
        'api-key': AZURE_SEARCH_API_KEY
    }

    try:
        response = requests.post(azure_search_url, headers=azure_cognitive_headers, data=json.dumps(docList))
        if response.status_code > 299:
            print(f"Error uploading issues to Azure Cognitive Search. StatusCode: {response.status_code}")
            print(response.text)
            return False
        else:
            print(f"Successfully uploaded payload to Azure Cognitive Search. StatusCode: {response.status_code}")
            return True
    except Exception as e:
        print(f"An error encountered uploading to Cognitive. Exception: {str(e)} StackTrace: {str(e.__traceback__)}")
        return False

def push_issues_to_azure_search(owner, repo, issues) -> int:
    uploaded_count = 0

    batch_size = 100 # Number of issues to upload in a single batch
    batch:list[GitHubIssueForTagging] = []

    for issue in issues:

        # Create an object of type GitHubIssueForTagging for each issue to upload.
        issueToUpload = GitHubIssueForTagging(
            id = issue["id"],
            issueId = issue["number"],
            issueUrl = issue["html_url"],
            owner = owner,
            repository = repo,
            title = issue["title"],
            body = issue["body"],
            comments = issue["comments"],
            labels = ",".join(label["name"] for label in issue["labels"]),
            state = issue["state"],
            descriptiveSummary = issue.get("oAI_descriptive_summary"),
            createdTimestamp = issue["created_at"],
            lastUpdatedTimestamp = issue["updated_at"]
        )
        issueToUpload.init_summaryVector()
        #print(json.dumps(issueToUpload, indent=2, default=GitHubIssueForTagging_json_encoder_for_display))
        #input("Press Enter to continue...")
        batch.append(issueToUpload)

        if len(batch) >= batch_size:
            # Upload the batch of issues
            batch_to_upload = {
                "value": [
                    {
                        "@search.action": "mergeOrUpload",
                        **issue_obj.to_dict()  # Unpack all fields from the GitHubIssueForTagging
                    } for issue_obj in batch
                ]
            }
            #print(json.dumps(batch_to_upload, indent=2))
            #input("Ready to upload. Press Enter to continue...")
            try:
                if try_batch_update_cognitive_index(owner, repo, batch_to_upload):
                    uploaded_count += len(batch)
                    batch = []
            except Exception as e:
                print(f"An error encountered uploading document with issueIds {','.join(issue.issueId for issue in batch)}. Exception: {str(e)} StackTrace: {str(e.__traceback__)}")

    # Upload any remaining issues in the batch
    if len(batch) > 0:
        batch_to_upload = {
                "value": [
                    {
                        "@search.action": "mergeOrUpload",
                        **issue_obj.to_dict()  # Unpack all fields from the GitHubIssueForTagging
                    } for issue_obj in batch
                ]
            }
        #print(json.dumps(batch_to_upload, indent=2))
        #input("Ready to upload. Press Enter to continue...")
        try:
            if try_batch_update_cognitive_index(owner, repo, batch_to_upload):
                uploaded_count += len(batch)
                batch = []
        except Exception as e:
            print(f"An error encountered uploading document with issueId {','.join(issue.issueId for issue in batch)}. Exception: {str(e)} StackTrace: {str(e.__traceback__)}")

    return uploaded_count

def get_githubIssues(owner, repo, headers, skip_upload:bool, skip_summarization:bool, resume:bool) -> list:
    # GitHub API endpoint to retrieve issues
    github_issues_url = "https://api.github.com/repos/{owner}/{repo}/issues"
    issues = []
    pageNumber = 1
    upload_count_so_far = 0
    # Check if a file names issues_errors.txt exists. If it does, read the contents and store them in a set.
    if not os.path.exists("issues_errors.txt"):
        with open("issues_errors.txt", "w") as f:
            f.write("")

    # Set to keep track of uploaded issues, holds id of the issues that have been uploaded to Azure Search

    if not skip_upload and resume == False:
        delete_cognitive_index(owner, repo)

    if not skip_upload:
        create_cognitive_index_if_not_exists(owner, repo)

    batch_of_issues_to_upload = []

    while True:
        sleep(1)  # Sleep for 1 second to avoid hitting the rate limit
        print(f"===========Fetching issues for repository: {repo} Page#: {pageNumber}")
        response = requests.get(github_issues_url.format(owner=owner, repo=repo) + "?state=all&sort=updated&per_page=100&page=" + str(pageNumber), headers=headers)
        if response.status_code != 200:
            print(f"Error fetching github issues. StatusCode: {response.status_code}")
            break

        issues_page = json.loads(response.text)
        if not issues_page:
            break

        # Filter out pull requests
        issues_page = [issue for issue in issues_page if 'pull_request' not in issue]

        issues_to_process_from_current_page = []
        consecutive_not_updated_issues = 0
        for issue in issues_page:
            if issue["body"] is None or issue["body"] == "":
                issue["body"] = ''
            ignore_reason = ignore_issue(owner, repo, issue, headers)
            if ignore_reason is not None:
                if ignore_reason == "IssueNotUpdated":
                    consecutive_not_updated_issues += 1
                continue
            else:
                if consecutive_not_updated_issues > 98:
                    if len(batch_of_issues_to_upload) > 0 and not skip_upload:
                        upload_count = push_issues_to_azure_search(owner, repo, batch_of_issues_to_upload)
                        upload_count_so_far += upload_count
                        print(f"Pushed {upload_count} issues out of {len(batch_of_issues_to_upload)} valid issues. Total issues in page: {len(issues_page)}  Page#: {pageNumber}. Total uploaded: {upload_count_so_far}")

                        print(f"Total Issues: {len(issues)} Pages: {pageNumber-1}")

                    print(f"Terminating: Reached 100 consecutive issues that have not been updated. Stopping processing.")
                    return issues

                consecutive_not_updated_issues = 0
                print(f"Processing issue: {issue['html_url']}")
                issue["body"] = summarize_user_provided_image_if_present(issue["body"])
                if issue["comments"] > 0:
                    issue["comments"] = get_issue_comments(issue["comments_url"], headers)
                else:
                    issue["comments"] = []

                #To do: Handle transferred issues by looking at timeline.
                # If the issue was transferred, then you need to upload the issue to the index that corresponds to source repo and summarization should include comments only uptill transfertime+15min and a detailed reason for transfer()
                # Will need to generate a new summary by ensuring the comments only include the comments up to the transfer time

                try:
                    if not skip_summarization:
                        issue["oAI_descriptive_summary"] = get_issue_summarization(issue["title"], issue["body"], issue["comments"])

                    # print(json.dumps(issue, indent=2, default=GitHubIssueForTagging_json_encoder_for_display))
                    # input("Summarization complete. Press Enter to continue...")
                    issues_to_process_from_current_page.append(issue)
                except  Exception as e:
                    print(f"===> Error processing issue {issue['html_url']}")
                    print(str(e))
                    # write the error to a file
                    with open("issues_errors.txt", "a") as f:
                        f.write("=========================\n")
                        f.write(f"Error processing issue with URL : {issue['html_url']}: {str(e)}\n")
                        f.write(f"Exception: {str(e)}\n")
                        f.write("=========================\n")

        issues.extend(issues_to_process_from_current_page)

        if not skip_upload:
            batch_of_issues_to_upload.extend(issues_to_process_from_current_page)
            if len(batch_of_issues_to_upload) >= 25:
                upload_count = push_issues_to_azure_search(owner, repo, batch_of_issues_to_upload)
                upload_count_so_far += upload_count
                print(f"Pushed {upload_count} issues out of {len(batch_of_issues_to_upload)} valid issues. Total issues in page: {len(issues_page)}  Page#: {pageNumber}. Total uploaded: {upload_count_so_far}")
                batch_of_issues_to_upload = []
            else:
                print(f"Batch size not reached at the end of Page# {pageNumber}. Current batch size: {len(batch_of_issues_to_upload)}. Waiting for more issues to process...")
        else:
            print(f"Found {len(issues_to_process_from_current_page)} valid issues out of {len(issues_page)} on Page#: {pageNumber}")

        pageNumber += 1

    if len(batch_of_issues_to_upload) > 0 and not skip_upload:
        upload_count = push_issues_to_azure_search(owner, repo, batch_of_issues_to_upload)
        upload_count_so_far += upload_count
        print(f"Pushed {upload_count} issues out of {len(batch_of_issues_to_upload)} valid issues. Total issues in page: {len(issues_page)}  Page#: {pageNumber}. Total uploaded: {upload_count_so_far}")

    print(f"Total Issues: {len(issues)} Pages: {pageNumber-1}")
    return issues


# GitHub repository information
owner = GITHUB_ORG
repo = GITHUB_REPO_NAME

# GitHub API token (optional)
headers = {'Authorization': f'Token {GITHUB_PAT_TOKEN}'}
resumeMode = True  # Set to True to enable resume mode
issuesList = get_githubIssues(owner, repo, headers, skip_upload=False, skip_summarization=False, resume=resumeMode)
print(f"Operation complete. Total issues found {len(issuesList)}")