---
mode: agent
---

## first 
modify the kurz project so the the serivce listens to the host name 'g' inseatd of 'g.io' and update the installation script accordingly so that the hosts file gets updated to point 'g' to localhost. Also, ensure that the project is set up to handle requests to 'g' without any issues related to SSL or domain validation. The installation script should include commands to modify the hosts file on the local machine to redirect 'g'

## second 

the service should be able to receive the PR id as base 62 and decode it before redirecting to ADO.

The service should detect if the provided id is in base 62 or decimal format and handle it accordingly. If the id is in base 62, it should decode it to decimal before redirecting to ADO. If the id is in decimal, it should redirect directly without any conversion.

it probably should invoke that logic only for /pr/{id} routes. At the moment it routes any suffix paths to ADO.

## third 
modify the gapir tool to use base 62 ids when printing the URLs with PR ids.

## last
update documents and tests and sample code to reflect the changes made in the service and gapir tool.