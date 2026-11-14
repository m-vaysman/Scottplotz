using Scottplotz.Hosting;

namespace Scottplotz.Tests.Hosting;

public sealed class ContentRootTests
{
    [Fact]
    public void A_program_folder_with_its_pages_is_the_content_root()
    {
        var folder = Path.Combine("C:", "Program Files", "Scottplotz");

        var root = ContentRoots.Installed(folder, path => path == Path.Combine(folder, "wwwroot"));

        Assert.Equal(folder, root);
    }

    [Fact]
    public void A_program_folder_without_pages_keeps_the_default_content_root()
    {
        Assert.Null(ContentRoots.Installed(Path.Combine("repo", "bin", "Debug"), _ => false));
    }

    [Fact]
    public void The_options_carry_the_arguments()
    {
        var args = new[] { "--urls", "http://127.0.0.1:0" };

        Assert.Same(args, ContentRoots.Options(args).Args);
    }
}
