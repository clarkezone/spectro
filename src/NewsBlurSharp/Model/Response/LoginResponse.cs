namespace NewsBlurSharp.Model.Response
{
    public class LoginResponse
    {
        public bool IsSuccess { get; }
        public string AuthCookieToken { get; }
        public int UserId { get; }

        internal LoginResponse(
            bool isSuccess, 
            string authCookieToken,
            int userId)
        {
            IsSuccess = isSuccess;
            AuthCookieToken = authCookieToken;
            UserId = userId;
        }
    }

    public class SignupResponse : LoginResponse
    {
        internal SignupResponse(bool isSuccess, string authCookieToken, int userId) 
            : base(isSuccess, authCookieToken, userId)
        {
        }
    }
}
