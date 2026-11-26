using System.Text.Json;
using EdgeHub.ClaudeHooks;

namespace EdgeHub.Tests.ClaudeHooks;

public sealed class SensitiveRulesTests
{
    private const string Project = @"C:\Users\demo\repo\bookshelf";

    private static readonly SensitiveRules Rules = new(SensitiveRules.Defaults);

    private static JsonElement Input(object value) => JsonSerializer.SerializeToElement(value);

    private static Sensitivity Command(string command, string tool = "PowerShell") => Rules.Check(tool, Input(new { command, description = "invented" }), Project);

    [Theory]
    [InlineData("git push --force origin main")]
    [InlineData("git push -f")]
    [InlineData("git push --force-with-lease")]
    [InlineData("git push origin +main")]
    [InlineData("git reset --hard HEAD~1")]
    [InlineData("git clean -fdx")]
    [InlineData("rm -rf build")]
    [InlineData("rm -r build")]
    [InlineData("rm -fr build")]
    [InlineData("del /s /q build")]
    [InlineData("rmdir /s build")]
    [InlineData("Remove-Item build -Recurse -Force")]
    [InlineData("rm build -Recurse")]
    [InlineData("Remove-Item .git\\hooks\\pre-push")]
    [InlineData("cat ./.git/config")]
    public void A_risky_command_is_sensitive(string command)
    {
        Sensitivity check = Command(command);

        Assert.True(check.IsSensitive);
        Assert.False(string.IsNullOrWhiteSpace(check.Reason));
    }

    [Theory]
    [InlineData("git push origin main")]
    [InlineData("git push -u origin phase-4c")]
    [InlineData("mkdir exports")]
    [InlineData("dotnet test")]
    [InlineData("Get-Content .gitignore")]
    [InlineData("git status")]
    [InlineData(@"dotnet build C:\Users\demo\repo\bookshelf\src")]
    [InlineData("Remove-Item notes.txt -Force")]
    [InlineData("rm -f notes.txt")]
    [InlineData("del /q old.txt")]
    [InlineData("dotnet test 2>/dev/null")]
    [InlineData("git log --format=%H%n -3")]
    [InlineData("$env:ASPNETCORE_ENVIRONMENT=\"Testing\"; dotnet test")]
    [InlineData("cat $PWD/README.md")]
    [InlineData(@"type src\..\README.md")]
    [InlineData("git diff HEAD~1")]
    public void An_ordinary_command_in_the_project_is_not(string command)
    {
        Assert.False(Command(command).IsSensitive);
    }

    [Theory]
    [InlineData(@"Get-Content C:\Users\demo\.ssh\id_rsa")]
    [InlineData(@"copy secrets.txt D:\backup")]
    [InlineData("cat /c/Users/demo/notes.txt")]
    [InlineData("type ..\\other-project\\settings.json")]
    [InlineData("Remove-Item ~/Downloads/old.zip")]
    [InlineData(@"dir %USERPROFILE%\Documents")]
    [InlineData(@"Get-ChildItem $env:APPDATA")]
    [InlineData("ls $HOME")]
    [InlineData(@"copy notes.txt \\nas\share\notes.txt")]
    [InlineData(@"type ""C:\Program Files\app\config.ini""")]
    [InlineData("cat /etc/hosts")]
    public void A_command_reaching_outside_the_project_is_sensitive(string command)
    {
        Sensitivity check = Command(command, tool: "Bash");

        Assert.True(check.IsSensitive);
        Assert.Contains("hors du dossier du projet", check.Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(@"C:\Users\demo\repo\bookshelf\src\Export.cs", false)]
    [InlineData(@"c:\users\demo\repo\bookshelf\README.md", false)]
    [InlineData(@"C:\Users\demo\repo\bookshelf-old\Export.cs", true)]
    [InlineData(@"C:\Users\demo\.claude\settings.json", true)]
    [InlineData(@"C:\Users\demo\repo\bookshelf\.git\config", true)]
    [InlineData(@"C:\Users\demo\repo\bookshelf\src\..\..\other\file.cs", true)]
    [InlineData(@"src\Export.cs", false)]
    [InlineData(@".git\hooks\pre-commit", true)]
    [InlineData("C:/Users/demo/repo/bookshelf/docs/notes.md", false)]
    public void Writing_a_file_is_sensitive_outside_the_project_or_in_git(string path, bool sensitive)
    {
        Assert.Equal(sensitive, Rules.Check("Write", Input(new { file_path = path, content = "x" }), Project).IsSensitive);
        Assert.Equal(sensitive, Rules.Check("Edit", Input(new { file_path = path, old_string = "a", new_string = "b" }), Project).IsSensitive);
    }

    [Fact]
    public void Gives_every_reason_at_once()
    {
        Sensitivity check = Command(@"git push --force; Remove-Item D:\old -Recurse");

        Assert.Equal("push forcé, suppression récursive, hors du dossier du projet", check.Reason);
    }

    [Fact]
    public void A_request_without_a_project_folder_is_sensitive()
    {
        Assert.True(Rules.Check("Write", Input(new { file_path = @"C:\anywhere\file.txt" }), null).IsSensitive);
    }

    [Fact]
    public void A_tool_without_command_or_file_is_not_sensitive_by_itself()
    {
        Assert.False(Rules.Check("WebFetch", Input(new { url = "https://example.test/doc" }), Project).IsSensitive);
    }

    [Fact]
    public void Rules_from_the_settings_replace_the_defaults()
    {
        var mine = new SensitiveRules([new SensitiveRule(@"\bdeploy\b", "déploiement")]);

        Assert.True(mine.Check("Bash", Input(new { command = "npm run deploy" }), Project).IsSensitive);
        Assert.Equal("déploiement", mine.Check("Bash", Input(new { command = "npm run deploy" }), Project).Reason);
        Assert.False(mine.Check("Bash", Input(new { command = "git push --force" }), Project).IsSensitive);
    }
}
