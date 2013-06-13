using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SpringloopsToGitHub
{
    [TestClass]
    public class TransferRunner
    {
        [TestMethod]
        public void DoTransfer()
        {

            /*
             * START CONFIG SECTION - modify the stuff in this section to your specific info
             */

            long ReportID = 0; //The Id of the report to migrate issues from in springloops
            string SLCookieValue = ""; //this is the token id in the springloops session, use a tool like fiddler to sniff this after logging into SL the cookies name should be SLS2_SESSIDv4

            var ghRepoOwner = "";//owner name of the repo
            var ghRepoName = "";//name of repo to add issues
            var ghRepoOwnerPassword = ""; //password of the repo owner

            //define the connection info of the repo owner used to add the users to the project
            GithubRestApiClient RepoOwner = new GithubRestApiClient().WithAuthentication(new HttpBasicAuthenticator(ghRepoOwner, ghRepoOwnerPassword));
           

            //Define all known users, if a user is known they will be used for creating issues, comments, assignments, and anywhere their name is found will be converted to an @GHUserID
            var KnownUsers = new List<ConversionInfo>();
            KnownUsers.Add(new ConversionInfo(
                "john",    ///the name of the user in springloops, also anywhere this name is in comments/messages it will be switched to @ghusername
                "john@gmail.com", //the email used at springloops for this user (this is how we know who added comments or created an issue)
                "johnnyGH",  //the users gh user account
                "golions!"  //the users gh password (i know it sucks that you need this, but this script doesn't support keys, fix it for us!
            ));
            //Add for every user in your springloops account

                
            //This GH connection is used if the user that the SL action occured with can't be determined.  It can be one of the known users, or a completely difference connection
            //This is also used to create dummy issues where SL has a sequence gap in issue numbers.  it defaults to the repo owner
            string FallBackUserID = ghRepoOwner;
            string FallBackUserPW = ghRepoOwnerPassword;
            GithubRestApiClient FallbackUser = new GithubRestApiClient().WithAuthentication(new HttpBasicAuthenticator(FallBackUserID, FallBackUserPW));


            /*
             * END CONFIG SECTION - EVERYTHING AFTER THIS SHOULDN'T NEED CHANGES!!
             */

            var result = RepoOwner.AddCollaborator(ghRepoOwner, ghRepoName, FallBackUserID);
            if (result.StatusCode != HttpStatusCode.NoContent)
                throw new Exception("couldn't add fallback user as collaborator");

            //Add all known users and fallback user as contributors to github project
            foreach (var user in KnownUsers)
            {
                var res = RepoOwner.AddCollaborator(ghRepoOwner, ghRepoName, user.GitHubUser);
                if (res.StatusCode != HttpStatusCode.NoContent)
                    throw new Exception("couldn't add collaborator");
            }




            var slclient = new SpringloopsAPIClient(Url).WithAuthentication(new cookieauth(SLCookieValue));

            

            var result2 = slclient.GetTaskSummaries<TaskReportSummary>(ReportID);
            var ordered = from t in result2.Data.Tickets
                          orderby t.RelativeId
                          select t.Id;

            long LastGHID = 0;//assumption...
            foreach (var id in ordered)
            {
                var SLTask = slclient.GetTask<TaskWrapper>(id).Data.Ticket;
                while ((LastGHID+1) < SLTask.RelativeId)
                {
                    //make dummy tickets & close them
                    var DummyGHIssue = FallbackUser.CreateIssue<Issue>(ghRepoOwner, ghRepoName, "filler ticket " + (LastGHID + 1).ToString(), "Filler Ticket to line up issue numbers", null, new string[] { "Filler" });
                    if (DummyGHIssue.StatusCode != HttpStatusCode.Created)
                        throw new Exception("Failed to create filler issue");
                    
                    LastGHID = DummyGHIssue.Data.Number;
                    if (LastGHID > SLTask.RelativeId)
                    {
                        throw new Exception("issue number in gh exceeds issue number in springloops");
                    }

                    var res = FallbackUser.EditIssue<Issue>(ghRepoOwner, ghRepoName, DummyGHIssue.Data.Number, State: "closed");
                    if (res.StatusCode != HttpStatusCode.OK)
                        throw new Exception("Failed to close filler issue");
                }
                


                //now make the real issue

                //get the opener
                var openerInfo = KnownUsers.SingleOrDefault(x => x.SLEmail == SLTask.Opener.Email.ToLower());
                if (openerInfo == null)
                    throw new Exception(SLTask.Opener.Email);

                
                var body = SLTask.Desc;
                //look in body for any mentions of known user, if they are there, replace with @
                if (body != null)
                {
                    foreach (var info in KnownUsers)
                    {
                        //assumes the name has no regex characters
                        body = Regex.Replace(body, info.Name, "@" + info.GitHubUser, RegexOptions.IgnoreCase);
                    }
                }

                var assignees = from a in SLTask.Assignment
                                select a.ShortName;

                body += "\n\nLegacy Transfer Info" +
                    "\n> Issue #: " + SLTask.RelativeId +
                    "\n> Global #: " + SLTask.Id +
                    "\n> Opener: " + SLTask.Opener.ShortName + 
                    "\n> Status:" + SLTask.Status.Name + 
                    "\n> Created:" + SLTask.Created.ToString() + 
                    "\n> Assignees:" + string.Join(", ", assignees.ToArray()) +
                    ""
                    ;
                var labels = from l in SLTask.TicketLabels
                             select l.Name;

                string assignTo = null;
                //Is it assigned to anyone?
                if (SLTask.Assignment.Count > 0)
                {
                    //github only allows asignment to one personunlike sl, we'll just grab the first
                    var assignee = KnownUsers.FirstOrDefault(x => x.SLEmail == SLTask.Assignment.First().Email.ToLower());
                    if (assignee != null)
                        assignTo = assignee.GitHubUser;
                }

                var ghissue = openerInfo.GHClient.CreateIssue<Issue>(ghRepoOwner, ghRepoName, SLTask.Title, body, assignTo, labels.ToArray());
                if (ghissue.StatusCode != HttpStatusCode.Created)
                    throw new Exception("Failed to create issue");
                
                LastGHID = ghissue.Data.Number;
                //close it if it's closed
                if (!SLTask.Status.IsOpen)
                {
                    //no way to know who closed it, just use crimedar account to close
                    var res = FallbackUser.EditIssue<Issue>(ghRepoOwner, ghRepoName, ghissue.Data.Number, State: "closed");
                    if (res.StatusCode != HttpStatusCode.OK)
                        throw new Exception("Failed to close issue");
                }

                //Add Comments
                var comments = from u in SLTask.Updates
                               where u.Svn == 0 //Ignore updates from svn
                               where !String.IsNullOrWhiteSpace(u.Comment)
                               orderby u.Created
                               select u;

                foreach (var comment in comments)
                {

                    var commenter = KnownUsers.SingleOrDefault(x => x.SLEmail == comment.Owner.Email.ToLower());
                    if (commenter == null)
                        throw new Exception(comment.Owner.Email);

                    //TODO: look in the body for any mentions of ryan, pete, or chris and replace with @
                    var text = comment.Comment;
                    //look in body for any mentions of known user, if they are there, replace with @
                    foreach (var info in KnownUsers)
                    {
                        //assumes the name has no regex characters
                        text = Regex.Replace(text, info.Name, "@" + info.GitHubUser, RegexOptions.IgnoreCase);
                    }

                    text += "\n\nLegacy Transfer Info" +
                    "\n> Created:" + comment.Created.ToString() +
                    ""
                    ;

                    var res = commenter.GHClient.CreateComment<IssueComment>(ghRepoOwner, ghRepoName, ghissue.Data.Number, text);
                    if (res.StatusCode != HttpStatusCode.Created)
                        throw new Exception("Failed to create comment");
                }

                
            }
                          

        }


    }


    public class ConversionInfo
    {
        public ConversionInfo(string Name, string SLEmail, string GitHubUser, string GitHubPassword)
        {
            this.Name = Name;
            this.SLEmail = SLEmail;
            this.GitHubUser = GitHubUser;
            this.GitHubPassword = GitHubPassword;
            this.GHClient = new GithubRestApiClient().WithAuthentication(new HttpBasicAuthenticator(this.GitHubUser, this.GitHubPassword));

        }

        public string Name { get; private set; }

        /// <summary>
        /// Spring Loops Email
        /// </summary>
        public string SLEmail { get; private set; }
        public string GitHubUser { get; private set; }
        public string GitHubPassword { get; private set; }


        public GithubRestApiClient GHClient { get; private set; }
    }


    public class cookieauth : IAuthenticator
    {

        private string token { get; set; }

        public cookieauth(string Token)
        {
            this.token = Token;
        }

        public void Authenticate(IRestClient client, IRestRequest request)
        {
            request.AddCookie("SLS2_SESSIDv4", this.token);
        }
    }
}
