# appsvc-fnc-dev-userstats

This function get some user and group data with exception.
This conect to a webpart where the data will be transform in diagramm to give some overview stats on the onboarding.

Unserstats return GUID and createdDateTime

Groupstat return Displayname, countNumber (number of member), group id, creation date time, description, and group type.

ActiveUsersStats return the total number of active user in the last 30 days

To be able to connect, you need some function config:

- clientId: The application registration create for
- clientSecret: Secret of the application registration
- tenantid: Tenant id where the app is
- exceptionGroupsArray: list seperate by , of group id that we don't want to return 
- exceptionUsersArray: list seperate by , of user id that we don't want to return 
- workspaceId: Id of the workspace in Log Analytics
