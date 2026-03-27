namespace Commerce.Api;

public static class ApiEndpoints
{
    private const string ApiBase = "api";

    // ── Products Endpoints ─────────────────────────────────────────────────
    public static class Products
    {
        private const string Base = $"{ApiBase}/products";
        
        public const string Get = $"{Base}/{{id:guid}}";
        public const string GetAll = Base;
    }

    // ── Product-Images Endpoints ─────────────────────────────────────────────────
    public static class ProductImages
    {
        private const string Base = $"{ApiBase}/products/{{productId:guid}}/images";

        public const string GetImage = $"{Base}/{{imageId:guid}}";
    }
    
    // ── Admin Endpoints ─────────────────────────────────────────────────
    public static class Admin
    {
        private const string Base = $"{ApiBase}/admin/products";
        
        public const string PostProduct = Base;
        public const string PutProduct = $"{Base}/{{id:guid}}";
        public const string DeleteProduct = $"{Base}/{{id:guid}}";

        public const string PostImage  = $"{Base}/{{productId:guid}}/images";
        public const string DeleteImage = $"{Base}/{{productId:guid}}/images/{{imageId:guid}}";
        public const string SetPrimary  = $"{Base}/{{productId:guid}}/images/{{imageId:guid}}/set-primary";
    }
    
    // ── Auth Endpoints ─────────────────────────────────────────────────
    public static class Auth
    {
        private const string Base = $"{ApiBase}/auth";
        
        public const string Register = $"{Base}/register";
        public const string Login = $"{Base}/login";
        public const string Refresh = $"{Base}/refresh";
        public const string Logout = $"{Base}/logout";
        public const string LogoutAll = $"{Base}/logout-all";
        public const string ForgotPassword = $"{Base}/forgot-password";
        public const string ResetPassword = $"{Base}/reset-password";
    }
}