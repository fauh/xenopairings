using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xenopairings.Models;
using Xenopairings.Services.Auth;
using Xenopairings.Services.Email;
using Xenopairings.Tests.Infrastructure;
using Shouldly;

namespace Xenopairings.Tests.Services;

public class AuthServiceTests : IClassFixture<InMemoryDatabaseFixture>
{
    private readonly InMemoryDatabaseFixture _db;
    private readonly IPasswordHasher<User> _hasher = new PasswordHasher<User>();

    public AuthServiceTests(InMemoryDatabaseFixture db) => _db = db;

    private AuthService BuildSut() => new(
        _db.CreateDbContext(), _hasher,
        new NullEmailSender(),
        Options.Create(new EmailSettings { BaseUrl = "https://test.example" }),
        NullLogger<AuthService>.Instance);

    // ── Register ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterAsync_CreatesUser()
    {
        var sut = BuildSut();
        var user = await sut.RegisterAsync("alice@test.com", "password123");

        user.Id.ShouldNotBe(Guid.Empty);
        user.Email.ShouldBe("alice@test.com");
        user.PasswordHash.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task RegisterAsync_NormalisesEmailToLowercase()
    {
        var sut = BuildSut();
        var user = await sut.RegisterAsync("Bob@Test.COM", "password123");
        user.Email.ShouldBe("bob@test.com");
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_Throws()
    {
        var sut = BuildSut();
        await sut.RegisterAsync("carol@test.com", "password123");

        await Should.ThrowAsync<InvalidOperationException>(
            () => sut.RegisterAsync("carol@test.com", "different"));
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmailDifferentCase_Throws()
    {
        var sut = BuildSut();
        await sut.RegisterAsync("dave@test.com", "password123");

        await Should.ThrowAsync<InvalidOperationException>(
            () => sut.RegisterAsync("DAVE@TEST.COM", "different"));
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoginAsync_CorrectCredentials_ReturnsUser()
    {
        var sut = BuildSut();
        await sut.RegisterAsync("eve@test.com", "correct-password");

        var user = await sut.LoginAsync("eve@test.com", "correct-password");
        user.ShouldNotBeNull();
        user.Email.ShouldBe("eve@test.com");
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsNull()
    {
        var sut = BuildSut();
        await sut.RegisterAsync("frank@test.com", "correct-password");

        var user = await sut.LoginAsync("frank@test.com", "wrong-password");
        user.ShouldBeNull();
    }

    [Fact]
    public async Task LoginAsync_UnknownEmail_ReturnsNull()
    {
        var user = await BuildSut().LoginAsync("nobody@test.com", "password");
        user.ShouldBeNull();
    }

    [Fact]
    public async Task LoginAsync_EmailCaseInsensitive()
    {
        var sut = BuildSut();
        await sut.RegisterAsync("grace@test.com", "password123");

        var user = await sut.LoginAsync("GRACE@TEST.COM", "password123");
        user.ShouldNotBeNull();
    }

    // ── GetByEmail ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByEmailAsync_ExistingUser_ReturnsUser()
    {
        var sut = BuildSut();
        await sut.RegisterAsync("heidi@test.com", "password123");

        var user = await sut.GetByEmailAsync("heidi@test.com");
        user.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetByEmailAsync_Missing_ReturnsNull()
    {
        var user = await BuildSut().GetByEmailAsync("nobody@test.com");
        user.ShouldBeNull();
    }
}
