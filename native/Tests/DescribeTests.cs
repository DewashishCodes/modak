using System.Text.Json.Nodes;
using Modak;
using Xunit;

public class DescribeTests
{
    static JsonObject J(string json) => JsonNode.Parse(json)!.AsObject();

    [Fact]
    public void Activity_lines_match_the_electron_build()
    {
        Assert.Equal("Running npm test", Describe.Activity("Bash", J("""{"command":"npm test\nsecond"}""")));
        Assert.Equal("Editing checkout.ts", Describe.Activity("Edit", J("""{"file_path":"C:/x/checkout.ts"}""")));
        Assert.Equal("Reading a.cs", Describe.Activity("Read", J("""{"file_path":"C:\\x\\a.cs"}""")));
        Assert.Equal("Searching the web", Describe.Activity("WebSearch", null));
        Assert.Equal("mcp__thing", Describe.Activity("mcp__thing", null));
    }

    [Fact]
    public void Edit_preview_is_a_diff_and_bash_is_a_terminal()
    {
        var p = Describe.PreviewFor("Edit", J("""{"file_path":"C:/x/checkout.ts","old_string":"const total = 0","new_string":"const total = sum(items)"}"""))!;
        Assert.Equal("diff", p.Kind);
        Assert.Equal("checkout.ts", p.File);
        Assert.Equal([('-', "const total = 0"), ('+', "const total = sum(items)")], p.Lines);
        Assert.Equal(new Preview("term", Command: "npm test").Command, Describe.PreviewFor("Bash", J("""{"command":"npm test"}"""))!.Command);
        Assert.Null(Describe.PreviewFor("Read", J("""{"file_path":"x"}""")));
    }

    [Fact]
    public void Codex_apply_patch_gets_its_own_line_diff_and_question()
    {
        var input = J("""{"command":"*** Begin Patch\n*** Update File: src/app.py\n@@\n-x = 1\n+x = 2\n*** End Patch"}""");
        Assert.Equal("Editing app.py", Describe.Activity("apply_patch", input));
        Assert.Equal("edit app.py", Describe.Ask("apply_patch", input));
        Assert.Equal([('-', "x = 1"), ('+', "x = 2")], Describe.PreviewFor("apply_patch", input)!.Lines);
    }

    [Fact]
    public void Output_tail_keeps_the_last_non_empty_lines()
    {
        Assert.Equal(["a", "b", "ok 15 passing"], Describe.OutputTail(J("""{"stdout":"a\n\nb\nok 15 passing\n","stderr":""}""")));
        Assert.Equal(["plain"], Describe.OutputTail(JsonValue.Create("plain")));
    }

    [Fact]
    public void Ask_lines_for_the_approval_bar()
    {
        Assert.Equal("rm -rf build", Describe.Ask("Bash", J("""{"command":"rm -rf build"}""")));
        Assert.Equal("edit a.txt", Describe.Ask("Write", J("""{"file_path":"a.txt"}""")));
        Assert.Equal("use Task", Describe.Ask("Task", J("{}")));
    }
}
