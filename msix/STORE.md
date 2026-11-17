# Microsoft Store: how to publish StoryMaker

## Once (you)

1. Create a free individual developer account: <https://storedeveloper.microsoft.com> (ID and selfie check).
2. In Partner Center: **Apps and games → New product → MSIX or PWA app**, reserve the name **StoryMaker**
   (if it's taken, for example *StoryMaker for Lunar Journal*).
3. **Product management → Product identity**: copy the values into `msix\store-identity.json`:

```json
{ "IdentityName": "<Package/Identity/Name>",
  "Publisher": "<Package/Identity/Publisher, CN=...>",
  "PublisherDisplayName": "<Package/Properties/PublisherDisplayName>",
  "DisplayName": "<the reserved name>" }
```

## Each version

1. `.\tools\build-msix.ps1` → `dist\StoryMaker-<version>.0-x64.msix` (unsigned; the Store signs it).
2. Partner Center → the app → **Start submission**, upload the .msix under **Packages**.

## The listing (paste into Partner Center)

- **Category**: Productivity (or Entertainment)
- **Privacy policy**: https://lundqwist.se/storymaker/privacy
- **Website**: https://lundqwist.se/projects/lunar-journal
- **Support**: https://github.com/Lundqwist/StoryMaker/issues
- **Screenshots**: `Media\store-1.png`, `store-2.png`, `store-3.png` (1920 × 1087)
- **Age rating**: no violence, chat or purchases in the app; it sends text to the AI the user picks.
- **Restricted capability `runFullTrust`**, reason: *A desktop (WinForms) program that reads the World
  of Warcraft folder chosen by the user and saves chapters where the user chooses.*

**Notes for certification** (Submission options → Notes for certification):

> StoryMaker reads save files written by a World of Warcraft addon. Testers don't need the game or an AI
> account: when no saves are found, the front page shows a sample character, Brakka Emberhide. Click her
> card, keep both play sessions ticked, and press "Write chapter". The AI is set to "No AI: the sample
> chapter", which writes a prepared chapter into the box. It can then be edited (formatting bar, Preview)
> and saved with "Save as Markdown...". Posting to a website needs the author's own site and is turned
> off for the sample. Real AIs (Ollama on the PC, or Claude/OpenAI/Gemini with the user's own API key)
> are optional and set up in Settings.

**Short description**: Turns your World of Warcraft: Forever adventures into your characters' own memories.

**Description**:

StoryMaker reads the journal that the free Lunar Journal addon keeps while you play World of Warcraft:
Forever (the quests and who gave them, the people you met, how long you ran, rode and fished, where you
died) and lets an AI of your choosing write it as a chapter of your character's life, told by the
character, many years later, remembering.

• Pick a character and the play sessions the chapter should cover
• Choose the AI: Ollama (free, runs on your own PC), Claude, OpenAI, Gemini or any OpenAI-compatible server
• Edit the chapter as Markdown, with a formatting bar and a preview
• Add screenshots from the game and sounds
• Post it to your website as the next chapter, or save it as a Markdown file
• Navy and gold, or a light look
• Not playing yet? Try it with the built-in sample character, no AI needed

Open source (MIT): https://github.com/Lundqwist/StoryMaker. World of Warcraft is a trademark of
Blizzard Entertainment; this is a fan project.

**Keywords**: World of Warcraft, WoW, story, journal, roleplay, AI, Ollama
