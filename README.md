[![Discord Bots](https://top.gg/api/widget/status/986993814671614094.svg)](https://top.gg/bot/986993814671614094)

# RinthBot

- Discord bot which main feature is to announce new updates of projects on [Modrinth](https://modrinth.com/)

## Example image:
*TBA*

## Invite
- You can invite the bot by clicking [here](https://discord.com/api/oauth2/authorize?client_id=986993814671614094&permissions=537316416&scope=bot%20applications.commands)

## Features
- Search for Modrinth projects
- Sends message when one of your subscribed projects got an update
- Easy subscribe with buttons from search
- Custom channels! You can set per-project update channel

## Get Started
After you invite the bot to your server, it's time to set it up
*For most commands you need to have Administrator privilege or have 'Subs Moderator' role*
1. **Set-up a default update channel**
	- `/modrinth set-update-channel [channel]`
	- This will set the default update channel, when you subscribe from search this will be the channel where the updates are sent
2. **Subscribe to projects**
	- You can use `/modrinth subscribe [projectID] [custom channel]` this way you can directly subscribe to project using it's ID
		- You don't have to provide custom channel, then the default one will be used
	- **Easier** `/modrinth search [query]`
		- Where you can search for project with slug, ID or it's name
		- The subscribe button doesn't let you pick a channel, but it will use the default one, if you want custom channel for this project, use the `subscribe` command and the ID from the search
3. **Test your setup**
	- Use `/modrinth test-setup` which will test if the bot can send message to your default channel and give you feedback 

### Other commands
- `/modrinth list [Plain/Table]`
	- This will list all your subscribed projects, default is Plain which is simple text
	- Table will generate Markdown like table
- `/modrinth unsubscribe [projectID]`
	- Unsubscribe project - you can actually do that even from search

## TODOs
- [ ] Better UX overall
    - [x] Easy subscribe from search 
    - [x] Unsubscribe all projects command
    - [ ] Easier unsubscribing from list with buttons
    - [ ] Export/Import list of subscribed projects
    - [ ] Add config command which will have everything at hand
- [ ] Settings with different layouts of the update message
- [ ] Add ping role for updates
- [ ] Add option to have a custom role for managing subscribed projects
    - [x] Added static "Subs Manager" role
    - [ ] Add Custom role
- [ ] Multiple channels support (so you can set per project channel)
  - [x] Option to have custom channel for each project
  - [ ] Easier channel settings (with buttons and not command)
- [ ] Support for other services? (e.g. CurseForge)

## Bug reports / Feature suggestions
- Be aware that this bot is still in **early development** and it may not be 100% stable
- If something's not working and the bot is online, check [Modrinth's status page](https://status.modrinth.com/) as Modrinth can be offline
- For bug reports and feature suggestions start a [new issue](https://github.com/Zechiax/RinthBot/issues/new)

## Data collected
- This bot needs to store some data so it can provide you with the updates
- Data stored are
    - Guild ID
    - Update Channel ID
    - List of subscribed projects
- Additional data may be added in the future (like planned layout options etc.)
