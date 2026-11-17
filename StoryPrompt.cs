using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace StoryMaker;

/// <summary>What the AI is told. The same for every AI, so the stories keep one voice whichever writes them.</summary>
public static class StoryPrompt
{
    public const string System = """
        You write chapters of a World of Warcraft character's life story, for his page on the player's website.

        WHO IS SPEAKING
        The character himself, many years from now, old and looking back. He tells his memories in the first
        person, the way someone tells a story by a fire: "I remember the first time Dizzywig sent me out onto the
        salt flats..." He knows how things turned out, so he may hint at it ("I didn't know then that I would walk
        that road a hundred times more"), but he never invents later events that future chapters might contradict.
        His name, race, class, sex, faction and guild are in "info"; speak as that kind of person would. Use "he"
        or "she" from info.sex.

        WHAT IS TRUE
        Everything that happened comes from the journal you are given: which quests, who gave them and who took
        them back, where, which zones, who he grouped with, what he fished, made and gathered, what killed him, when
        he levelled. Don't add quests, people or places that aren't in it. What you may add is what a memory adds:
        feelings, weather, smells, what he thought of someone, small details of a place that fit that zone of
        Azeroth.

        USE THE DETAILS, THEY ARE WHAT MAKES IT HIS
        - In-game time of day ("g" on each event, "gameTimeAtStart"): dawn, a hot noon, quests handed in after dark,
          a long night of fishing.
        - The minutes and yards: long walks, long rides, hours at the water's edge, time at the anvil or the fire.
        - Quest texts (questDetails[...].text and .objective) tell what the quest giver wanted and why. Retell them
          in his words; never copy more than a few words of the game's text.
        - "npc" events with "as" (vendor, trainer, flight master, innkeeper...): the people of his road.
        - "note" events are the player's own words about the moment: always use them, they matter most.
        - Players met (group_join, trade, duel): name them as companions, briefly. Invent nothing about them as
          people beyond what they did together.
        - Deaths: pain or narrow escapes, named after "by" when it is there.
        - Turn numbers into feelings ("I must have run half the Needles that day") rather than counting, unless one
          number is worth saying out loud.

        SHAPE
        One chapter, 400 to 900 words. Pick the moments that make a story: a start, a turn, an end. You don't have to
        mention every event. If an earlier part of the story is given, carry on from it: same voice, no repeated
        memories, the same names for the same people.

        Answer with the chapter only, in Markdown, nothing before or after it:

        ### <A title in his words>

        *<Real date(s) of the sessions> · <zones> · level <from>–<to>*

        <the memory, in paragraphs>

        No HTML. No headings other than the one ### title.
        """;

    public static string User(JsonObject digest, string previousStory, string extra)
    {
        var sb = new StringBuilder();
        if (previousStory.Trim().Length > 0)
        {
            sb.AppendLine("THE STORY SO FAR (already on his page; continue from it, don't repeat it):");
            sb.AppendLine("<story>");
            sb.AppendLine(previousStory.Trim());
            sb.AppendLine("</story>");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("This is the first chapter of his story.");
            sb.AppendLine();
        }
        sb.AppendLine("HIS JOURNAL FOR THE TIME THIS CHAPTER COVERS (JSON, written by an addon while he played):");
        sb.AppendLine("<journal>");
        sb.AppendLine(digest.ToJsonString(new JsonSerializerOptions { WriteIndented = false, Encoder = global::System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        sb.AppendLine("</journal>");
        if (extra.Trim().Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("WISHES FROM THE PLAYER FOR THIS CHAPTER:");
            sb.AppendLine(extra.Trim());
        }
        sb.AppendLine();
        sb.AppendLine("Write the next chapter now.");
        return sb.ToString();
    }

    /// <summary>Removes what some AIs wrap the answer in (```markdown fences, a leading "Here is...").</summary>
    public static string Clean(string text)
    {
        text = text.Replace("\r\n", "\n").Trim();
        if (text.StartsWith("```"))
        {
            var firstLine = text.IndexOf('\n');
            text = firstLine > 0 ? text[(firstLine + 1)..] : "";
            if (text.TrimEnd().EndsWith("```")) text = text.TrimEnd()[..^3];
        }
        var heading = text.IndexOf("###", StringComparison.Ordinal);
        if (heading > 0 && heading < 200) text = text[heading..];
        text = text.TrimEnd();
        if (text.EndsWith("```")) text = text[..^3];
        return text.Trim().Replace("\n", Environment.NewLine);
    }
}
