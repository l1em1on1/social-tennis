using SocialTennis.Api.Roles;

namespace SocialTennis.Api.UnitTests;

/// <summary>The app must never be left with no Admin (issue #18).</summary>
public class AdminRoleRulesTests
{
    [Fact]
    public void The_only_Admin_cannot_be_demoted_when_no_address_is_configured()
    {
        var refusal = AdminRoleRules.CanRemoveAdmin(isAdminBySetting: false, anyAdminBySetting: false, otherGrantedAdmins: 0);

        Assert.Equal(AdminRoleRules.Refusal.LastAdmin, refusal);
    }

    [Fact]
    public void An_Admin_can_be_demoted_while_another_granted_Admin_remains()
    {
        var refusal = AdminRoleRules.CanRemoveAdmin(isAdminBySetting: false, anyAdminBySetting: false, otherGrantedAdmins: 1);

        Assert.Equal(AdminRoleRules.Refusal.None, refusal);
    }

    [Fact]
    public void The_only_granted_Admin_can_be_demoted_when_a_configured_address_can_still_sign_in()
    {
        var refusal = AdminRoleRules.CanRemoveAdmin(isAdminBySetting: false, anyAdminBySetting: true, otherGrantedAdmins: 0);

        Assert.Equal(AdminRoleRules.Refusal.None, refusal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void An_Admin_by_configuration_cannot_be_demoted_in_the_app_however_many_others_there_are(int others)
    {
        var refusal = AdminRoleRules.CanRemoveAdmin(isAdminBySetting: true, anyAdminBySetting: true, otherGrantedAdmins: others);

        Assert.Equal(AdminRoleRules.Refusal.AdminBySetting, refusal);
    }
}
