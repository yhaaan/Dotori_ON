namespace TeamOverlay.Supabase
{
    /// <summary>
    /// Public client configuration. A publishable key is intentionally safe to
    /// ship in a desktop build; authorization still comes from the user's JWT
    /// and database RLS. Elevated secret/service-role keys must never be added.
    /// </summary>
    public static class SupabaseProjectConfig
    {
        public const string ProjectRef = "pperuinfufndfathcosf";
        public const string ProjectUrl = "https://pperuinfufndfathcosf.supabase.co";
        public const string PublishableKey = "sb_publishable_XXTLgr_HJhoIAUt2_X_FgA_DOVeuLa9";
        public const string CredentialTarget =
            "ProjectDDD.TeamOverlay.SupabaseAuth." + ProjectRef;
    }
}
