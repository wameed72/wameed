namespace FormFlow.Web.Services
{
    public static class Policies
    {
        public const string Administrator = "Administrator";

        public const string Staff = "Staff";
    }

    public static class Claims
    {
        public const string IsAdministrator = "formflow:is-admin";

        public const string StageRole = "formflow:stage-role";
    }
}
