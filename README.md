SpringloopsToGithub
===================

Code and guide from migrating issues &amp; source from springloops SVN to github git.  I wanted to move from springloops to github for some projects and i didn't want to lose issues, issue #'s and of course source commit messages that reference isssues so i built this tool.


What it Does
============
* Brings over all issues & issue comments
* Attributes issues to original opener
* Atributes commetns to original author
* includes info about the SL issue like attachments asignees and status
* assigns issues to the right people
* closes issues
* maintains issue numbers
* creates dummy issues for missing numbers in issue number sequence in springloops (liek if you deleted an issue)
* replaces all mentioned names in issues with @githubuserid
* adds users as contributors

What it Does Not Do
===================
* bring over attachments.  Github only supports images (at least when i wrote this)
* automatically bring over source.  you must do a SVN to GIT conversion documented in this readme
* sequence comments and code commits.  All issues are created, all comments are addded, then all code is committed

Steps
=====
The transfer tool is build a VS unit test so you can step through it and test it out & tweak it easily.  these steps are for windows users.

1. download & install the free Visual Studio Express for Web 2012.
2. get fiddler http://fiddlertool.com or some similar tool because you'll need to sniff your http traffic
3. Create a brand new github repo
4. clone this repo (don't dl the zip as the submodules won't be there, and you need them)
5. open up this project's solution file SpringloopsToGitHub.sln
6. Rebuild the solution (you might get some compile errors, but i bet you can figure them out)
7. Open up the TransferRunner class in SpringloopstoGitHub solution project
8. In the DoTransfer test method there's a large config section where you have to input credentials and stuff, fill that all out
9. login to springloops with fiddler enabled and sniff the cookie to get the SLCookieValue
10. Use springloops issue system to get the All Issues report.  Use fiddler to sniff the ID of that report
11. I suggest setting a break point and stepping through the code and see issues appear in your repo
12. When satisfied with the execution delete your github repo and create a new one with the same name
13. Run the transfer
14. Delete the thousands of emails you'll get because of issues and mentions
15. Follow the steps in the svn to git guide to commit all code changes to github and see commits show up in issues


SVN to Git Guide
================
Just follow the steps from this excellent SO answer @ http://stackoverflow.com/a/3972103/442773 If your on windows getting cygwin can help.  Remeber do this AFTER issues have been migrated.
