using Yapair.Core.Auth;

namespace Yapair.Core.Tests;

public class AuthTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _keysFile;
    private readonly string _approvalsFile;

    public AuthTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "yapair_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _keysFile = Path.Combine(_tempDir, "api-keys.json");
        _approvalsFile = Path.Combine(_tempDir, "approvals.json");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); }
        catch { }
    }

    [Fact]
    public void UserRole_PermissionsHierarchy_WorksAsExpected()
    {
        Assert.True(UserRole.Admin.HasPermission(UserRole.Admin));
        Assert.True(UserRole.Admin.HasPermission(UserRole.Technician));
        Assert.True(UserRole.Admin.HasPermission(UserRole.ReadOnly));

        Assert.False(UserRole.Technician.HasPermission(UserRole.Admin));
        Assert.True(UserRole.Technician.HasPermission(UserRole.Technician));
        Assert.True(UserRole.Technician.HasPermission(UserRole.ReadOnly));

        Assert.False(UserRole.ReadOnly.HasPermission(UserRole.Admin));
        Assert.False(UserRole.ReadOnly.HasPermission(UserRole.Technician));
        Assert.True(UserRole.ReadOnly.HasPermission(UserRole.ReadOnly));
    }

    [Fact]
    public void ApiKeyManager_CreateAndValidate_Succeeds()
    {
        var mgr = new ApiKeyManager(_keysFile, masterEnvKey: "test_master_secret");
        var (rec, rawKey) = mgr.CreateKey("Tech1", UserRole.Technician);

        Assert.NotNull(rec);
        Assert.StartsWith("yap_live_", rawKey);
        Assert.Equal("technician", rec.Role);

        // Validate generated key
        var (valid, role, keyRec) = mgr.ValidateKey(rawKey);
        Assert.True(valid);
        Assert.Equal(UserRole.Technician, role);
        Assert.Equal(rec.KeyId, keyRec?.KeyId);

        // Validate master key
        var (mValid, mRole, _) = mgr.ValidateKey("test_master_secret");
        Assert.True(mValid);
        Assert.Equal(UserRole.Admin, mRole);

        // Validate invalid key
        var (badValid, _, _) = mgr.ValidateKey("wrong_key");
        Assert.False(badValid);
    }

    [Fact]
    public void ApiKeyManager_RotateKey_RevokesOldAndCreatesNew()
    {
        var mgr = new ApiKeyManager(_keysFile);
        var (oldRec, oldRaw) = mgr.CreateKey("AdminKey", UserRole.Admin);

        var rotateRes = mgr.RotateKey(oldRec.KeyId);
        Assert.NotNull(rotateRes);
        var (newRec, newRaw) = rotateRes.Value;

        Assert.NotEqual(oldRec.KeyId, newRec.KeyId);
        Assert.NotEqual(oldRaw, newRaw);

        // Old key should now be invalid
        var (oldValid, _, _) = mgr.ValidateKey(oldRaw);
        Assert.False(oldValid);

        // New key should be valid with Admin role
        var (newValid, newRole, _) = mgr.ValidateKey(newRaw);
        Assert.True(newValid);
        Assert.Equal(UserRole.Admin, newRole);
    }

    [Fact]
    public void ApiKeyManager_RevokeKey_InvalidatesKey()
    {
        var mgr = new ApiKeyManager(_keysFile);
        var (rec, raw) = mgr.CreateKey("TempKey", UserRole.ReadOnly);

        Assert.True(mgr.ValidateKey(raw).IsValid);

        var revoked = mgr.RevokeKey(rec.KeyId);
        Assert.True(revoked);

        Assert.False(mgr.ValidateKey(raw).IsValid);
    }

    [Fact]
    public void ApprovalManager_CreateApproveAndValidate_FlowSucceeds()
    {
        var mgr = new ApprovalManager(_approvalsFile);
        var req = mgr.CreateRequest("PC-DEV-01", "R3", "Printer spooler registry repair");

        Assert.NotNull(req);
        Assert.Equal("pending", req.Status);
        Assert.Equal(6, req.ApprovalCode?.Length);

        // Cannot validate while pending
        Assert.False(mgr.ValidateApprovalCode("PC-DEV-01", "R3", req.ApprovalCode!));

        // Approve
        var approved = mgr.Approve(req.Id, "admin_user");
        Assert.NotNull(approved);
        Assert.Equal("approved", approved.Status);
        Assert.Equal("admin_user", approved.ApprovedBy);

        // Validating approved code succeeds
        Assert.True(mgr.ValidateApprovalCode("PC-DEV-01", "R3", req.ApprovalCode!));

        // Wrong code fails
        Assert.False(mgr.ValidateApprovalCode("PC-DEV-01", "R3", "000000"));

        // Wrong mode fails
        Assert.False(mgr.ValidateApprovalCode("PC-DEV-01", "R2", req.ApprovalCode!));
    }
}
