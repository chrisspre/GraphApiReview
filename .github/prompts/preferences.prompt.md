---
mode: agent
---

i would like to add another dotnet cli tool under src that allows me to set the baffino preferences from the command line


the references are set via a PATCH request 
with URL https://graph.microsoft.com/v1.0/me/extensions/microsoft.teams.baffino

and this body. 
 
 ```JSON
{
    "timeAllocation": 0,
    "secondaryOnCallStrategy": "available",
    "privateNotifications": [
        "reviewAssignment",
        "voteReset"
    ],
    "onCallSkip": [
        ""
    ],
    "id": "microsoft.teams.baffino"
}
```

The time allocation value whould be controlled via a command line parameter.

Ehe application should use the following values for authentication

  "app_displayname": "Graph Explorer",
  "appid": "de8bc8b5-d9f9-48b1-a8ad-b748da725064",

the pattern how to get an access token can be found in src\gapir.core\ConsoleAuth.cs which uses 
MSAL brokered authentication ( `new BrokerOptions(BrokerOptions.OperatingSystems.Windows);` ) and caches the token via TokenCacheHelper class (src\gapir.core\TokenCacheHelper.cs).


Lets use System.CommandLine and start with GETting the preferences first


