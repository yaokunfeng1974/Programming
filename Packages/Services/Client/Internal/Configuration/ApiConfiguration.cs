using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http;


namespace Adecco.WW.Packages.WebApi.Services.Client.Internal.Configuration
{
    /// <summary>
    /// Abstract class representing the configuration settings for an API client.
    /// </summary>
    public abstract class ApiConfiguration : IBaseApiConfiguration
    {   
        #region Client Base Settings
        /// <summary>
        /// Gets or sets the Base URL for the API client. Default is null.
        /// </summary>
        [Required]
        public string BaseUrl { get; set; } = null;

        /// <summary>
        /// Gets or sets the timeout duration for HTTP requests. Default is 60 seconds.
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);
        #endregion

        #region Credentials
        /// <summary>
        /// Gets or sets the OAuth2() endpoint for authentication.
        /// This is typically a URL where the client can obtain an access token.
        /// </summary>
        public string CredentialsPath { get; set; } = null;
        /// <summary>
        /// Token type string for authentication (e.g., "Bearer").Default is "Bearer".
        /// this is used foreach request to set the Authorization header.
        /// </summary>
        public string CredentialsTokenType { get; set; } = "Bearer";
        /// <summary>
        /// Gets or sets the credentials used for authentication. This is typically a dictionary containing key-value pairs for authentication headers.
        /// </summary>
        public Dictionary<string, string> CredentialsDictionary { get; set; }
        /// <summary>
        /// Gets the authentication response fields.
        /// This is typically a dictionary containing key-value pairs for authentication response fields.
        /// This give the developer possibility to view the response we got when using OAuth2() method.
        /// this Dictionary will be overriden when we call OAuth2() method.
        /// </summary>
        public Dictionary<string, string> AuthenticationResponseDictionary { get; set; } = new();
        #endregion

        #region mTLS/Client Certificate Configuration
        /// <summary>
        /// Enables client certificate authentication for mTLS. Default is false.
        /// </summary>
        public bool UseClientCertificate { get; set; } = false;

        /// <summary>
        /// Path to the client certificate file (e.g., .pfx or .p12).
        /// Required when <see cref="UseClientCertificate"/> is true.
        /// </summary>
        public string ClientCertificatePath { get; set; }=null;

        /// <summary>
        /// Password for the client certificate file. Leave empty if not password-protected.
        /// </summary>
        public string ClientCertificatePassword { get; set; } = null;
        #endregion

        #region Resilience Policies
        #region Retry Policy

        /// <summary>
        /// Retry After Header Max In Seconds. Default is 3600.
        /// </summary>
        public TimeSpan RetryAfterHeaderMaxInSeconds { get; set; } = TimeSpan.FromSeconds(3600);
        /// <summary>
        /// Gets or sets the number of retry attempts for faulted requests (e.g., connectivity issues). Default is 3.
        /// </summary>
        public int FaultedRequestRetryCount { get; set; } = 3;

        /// <summary>
        /// Gets or sets the delay (in seconds) between retry attempts for faulted requests. Default is 2 seconds.
        /// </summary>
        public int FaultedRequestRetryDelay { get; set; } = 2;
        #endregion

        #region Circuit Breaker
        /// <summary>
        /// Gets or sets the duration (in seconds) the circuit breaker remains open after tripping. Default is 30 seconds.
        /// </summary>
        public int CircuitBreakerDurationSeconds { get; set; } = 30;
        #endregion
        #endregion

        #region Connection Management
        /// <summary>
        /// Gets or sets the delay after which a connection refresh is forced. Default is 300 seconds.
        /// </summary>
        public TimeSpan RefreshConnectionDelay { get; set; } = TimeSpan.FromSeconds(300);

        /// <summary>
        /// Gets or sets whether connections should be forcibly refreshed after <see cref="RefreshConnectionDelay"/>. Default is true.
        /// </summary>
        public bool ForceRefreshConnection { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of concurrent connections allowed. Null means no explicit limit. Default is null. 
        /// </summary>
        public int? ConcurrentConnectionLimit { get; set; } = null;

        /// <summary>
        /// Gets or sets the lifetime of pooled TCP connections before they are replaced. Default is 5 minutes.
        /// </summary>
        public TimeSpan PooledConnectionLifetime { get; set; } = TimeSpan.FromMinutes(5);
        #endregion

        #region HttpClientHandler Configuration
        /// <summary>
        /// Gets or sets whether the handler should automatically handle cookies. Default is true.
        /// </summary>
        public bool UseCookies { get; set; } = true;

        /// <summary>
        /// Gets or sets whether the handler should follow HTTP redirection responses. Default is true.
        /// </summary>
        public bool AllowAutoRedirect { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of automatic redirections to follow. Default is 50.
        /// </summary>
        public int MaxAutomaticRedirections { get; set; } = 50;

        /// <summary>
        /// Gets or sets whether a proxy should be used for requests.Default is false.
        /// </summary>
        public bool UseProxy { get; set; } = false;

        /// <summary>
        /// Gets or sets the proxy server URL when <see cref="UseProxy"/> is enabled. Default is null.
        /// </summary>
        public string ProxyUrl { get; set; }=null;

        /// <summary>
        /// Gets or sets the maximum concurrent connections allowed per server. Default is 200.
        /// </summary>
        public int MaxConnectionsPerServer { get; set; } = 200;

        /// <summary>
        /// Gets or sets the decompression methods (e.g., GZip, Deflate) used by the handler. Default is <see cref="DecompressionMethods.None"/>.
        /// </summary>
        public DecompressionMethods AutomaticDecompression { get; set; } = DecompressionMethods.None;
        #endregion

        #region Request Configuration
        /// <summary>
        /// Gets or sets default headers to include in all HTTP requests. Default is an empty dictionary.
        /// </summary>
        public Dictionary<string, string> DefaultRequestHeaders { get; set; } = new Dictionary<string, string>();
        #endregion

        #region Response Configuration
        /// <summary>
        /// Gets or sets the maximum buffer size for HTTP response content. Default is 2GB.
        /// </summary>
        public long MaxResponseContentBufferSize { get; set; } = 2147483647;
        #endregion

        #region HTTP Version Settings
        /// <summary>
        /// Gets or sets the default HTTP version for requests. Default is HTTP/1.1.
        /// </summary>
        public Version DefaultRequestVersion { get; set; } = HttpVersion.Version11;

        /// <summary>
        /// Gets or sets the policy for selecting HTTP versions. Default is <see cref="HttpVersionPolicy.RequestVersionOrLower"/>.
        /// </summary>
        public HttpVersionPolicy DefaultVersionPolicy { get; set; } = HttpVersionPolicy.RequestVersionOrLower;
        #endregion

        #region Advanced Connection Settings
        /// <summary>
        /// Gets or sets the maximum number of queued actions allowed (uses SocketsHttpHandler advanced configuration). Default is null (no limit).
        /// </summary>
        public int? MaxQueuingActions { get; set; } = null;
        #endregion

        #region Advanced Logging Setting
        /// <summary>
        /// Enable Logging Request Content. Default is true.
        /// </summary>
        public bool EnableLoggingRequestContent { get; set; } = true;
        /// <summary>
        /// Enable Logging Response Content. Default is true.
        /// </summary>
        public bool EnableLoggingResponseContent { get; set; } = true;
        /// <summary>
        /// Enable Redaction of Sensitive Data in Logs. Default is false. When enabled, sensitive data in headers and body will be replaced with the value specified in <see cref="RedactionText"/>.
        /// </summary>
        public bool EnableRedaction {get; set; } = false;
        /// <summary>
        /// Set or get Redaction Text to be used to replace Sensitive data.Default is ***REDACTED***.
        /// </summary>
        public string RedactionText { get; set; } = "***REDACTED***";
        /// <summary>
        /// Set or get Max Body string Log Length. if content exceed this value, i will be truncated with prefix : ...(truncated). 
        /// </summary>
        public int MaxBodyLogLength { get; set; } = 4096;
        /// <summary>
        /// Set Or Get Sensitive Headers HashSet to be used while logging Headers. Default is not Empty.
        /// </summary>
        public HashSet<string> SensitiveHeaders { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Authentication & Authorization
            "Authorization",
            "Proxy-Authorization",
            "X-Authorization",
            "X-API-Key",
            "X-API-Token",
            "X-Auth-Token",
            "X-Access-Token",
            "X-Secret-Token",
            "X-CSRF-Token",
            "X-XSRF-Token",
            "Access-Token",
            "Id-Token",
            "Refresh-Token",
            "Ocp-Apim-Subscription-Key",
            "Apim-Subscription-Key",
            "X-Apim-Subscription-Key",
            "api-key",
            "subscription-key",
            
            // Session & Cookies
            "Cookie",
            "Set-Cookie",
            "X-Session-Id",
            "Session-Id",
            "X-Session-Token",
            
            // Security Headers
            "X-Forwarded-For",
            "X-Real-IP",
            "X-Client-Certificate",
            "X-Client-Cert",
            "Client-Cert",
            "X-Client-Secret",
            "X-Secret",
            "X-Secret-Key",
            
            // Personal Identification
            "X-User-Id",
            "X-User-Name",
            "X-User-Email",
            "X-Customer-Id",
            "X-Patient-Id",
            "X-Employee-Id",
            
            // Financial Headers
            "X-Credit-Card",
            "X-Bank-Account",
            "X-Account-Number",
            "X-Routing-Number",
            
            // OAuth & OpenID
            "OAuth-Token",
            "OAuth-Secret",
            "OpenID-Token",
            "X-OAuth-Token",
            "X-OpenID-Token",
            
            // API Specific
            "X-API-Secret",
            "X-API-Key",
            "X-API-Signature",
            "X-Signature",
            "X-HMAC",
            "X-HMAC-Signature",
            
            // Cloud & Platform Specific
            "X-AWS-Secret",
            "X-Azure-Token",
            "X-GCP-Token",
            "X-Google-Token",
            "X-Facebook-Token",
            "X-Github-Token",
            "X-Twitter-Token",
            "X-LinkedIn-Token",
            
            // Custom Application Headers
            "X-App-Token",
            "X-App-Secret",
            "X-Application-Key",
            "X-Application-Secret",
            "X-Application-Token",
            
            // Webhook & Integration
            "X-Webhook-Secret",
            "X-Webhook-Token",
            "X-Callback-Token",
            "X-Integration-Token",
            
            // Encryption Keys
            "X-Encryption-Key",
            "X-Encryption-Secret",
            "X-PGP-Key",
            "X-PGP-Secret",
            
            // JWT Specific
            "X-JWT-Token",
            "X-JWT",
            "JWT-Token",
            
            // Two-Factor Authentication
            "X-2FA-Token",
            "X-2FA-Code",
            "X-TOTP",
            "X-TOTP-Token",
            
            // Password Reset
            "X-Password-Reset-Token",
            "X-Reset-Token",
            
            // Device Authentication
            "X-Device-Token",
            "X-Device-Secret",
            "X-Device-Id",
            
            // Mobile & Push
            "X-Push-Token",
            "X-Push-Secret",
            "X-FCM-Token",
            "X-APNS-Token",
            
            // Payment Processors
            "X-Stripe-Token",
            "X-Stripe-Secret",
            "X-Paypal-Token",
            "X-Paypal-Secret",
            "X-Braintree-Token",
            
            // Government & Compliance
            "X-SSN",
            "X-Social-Security",
            "X-Tax-ID",
            "X-National-ID",
            "X-Passport-Number",
            "X-Driver-License"
        };
        /// <summary>
        /// Set Or Get Sensitive Field Names HashSet to be used while logging.Content Default is not Empty.
        /// </summary>
        public HashSet<string> SensitiveFieldNames { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Authentication & Passwords
            "password",
            "pwd",
            "pass",
            "passphrase",
            "pin",
            "passcode",
            "secret",
            "Set-Cookie",
            "secretkey",
            "secret_key",
            "apikey",
            "api_key",
            "apisecret",
            "api_secret",
            "accesstoken",
            "access_token",
            "refreshtoken",
            "refresh_token",
            "bearertoken",
            "bearer_token",
            "authtoken",
            "auth_token",
            "jwttoken",
            "jwt_token",
            "oauthtoken",
            "oauth_token",
            "sessiontoken",
            "session_token",
            "sessionid",
            "session_id",
            "csrftoken",
            "csrf_token",
            "xsrftoken",
            "xsrf_token",
            "client_id",
            "client_secret",
            "grant_type",

            // Personal Identification
            "ssn",
            "socialsecurity",
            "social_security",
            "socialsecuritynumber",
            "social_security_number",
            "taxid",
            "tax_id",
            "nationalid",
            "national_id",
            "governmentid",
            "government_id",
            "passportnumber",
            "passport_number",
            "driverslicense",
            "drivers_license",
            "driverlicense",
            "driver_license",
            "license_number",
            "stateid",
            "state_id",
            
            // Contact Information
            "email",
            "emailaddress",
            "email_address",
            "phone",
            "phonenumber",
            "phone_number",
            "mobile",
            "mobilenumber",
            "mobile_number",
            "telephone",
            "cellphone",
            "cell_phone",
            "homephone",
            "home_phone",
            "workphone",
            "work_phone",
            
            // Address Information
            "address",
            "streetaddress",
            "street_address",
            "homeaddress",
            "home_address",
            "mailingaddress",
            "mailing_address",
            "billingaddress",
            "billing_address",
            "shippingaddress",
            "shipping_address",
            "zipcode",
            "zip_code",
            "postalcode",
            "postal_code",
            
            // Financial Information
            "creditcard",
            "credit_card",
            "cardnumber",
            "card_number",
            "ccnumber",
            "cc_number",
            "expiration",
            "expiry",
            "expirydate",
            "expiry_date",
            "cvv",
            "cvc",
            "securitycode",
            "security_code",
            "bankaccount",
            "bank_account",
            "accountnumber",
            "account_number",
            "routingnumber",
            "routing_number",
            "iban",
            "swift",
            "swiftcode",
            "swift_code",
            "bic",
            "credit_score",
            "creditscore",
            
            // Personal Details
            "firstname",
            "first_name",
            "lastname",
            "last_name",
            "fullname",
            "full_name",
            "middlename",
            "middle_name",
            "maidenname",
            "maiden_name",
            "dateofbirth",
            "date_of_birth",
            "birthdate",
            "birth_date",
            "birthday",
            "age",
            "gender",
            "maritalstatus",
            "marital_status",
            
            // Medical & Health
            "medicalrecord",
            "medical_record",
            "patientid",
            "patient_id",
            "healthinsurance",
            "health_insurance",
            "insuranceid",
            "insurance_id",
            "medicare",
            "medicaid",
            "diagnosis",
            "treatment",
            
            // Employment & Education
            "employer",
            "employername",
            "employer_name",
            "employeraddress",
            "employer_address",
            "salary",
            "income",
            "wage",
            "pay",
            "compensation",
            "employeeid",
            "employee_id",
            "studentid",
            "student_id",
            
            // Digital Identifiers
            "ipaddress",
            "ip_address",
            "macaddress",
            "mac_address",
            "deviceid",
            "device_id",
            "imei",
            "imsi",
            "serialnumber",
            "serial_number",
            "uuid",
            "guid",
            
            // Security Questions
            "mothermaidenname",
            "mother_maiden_name",
            "securityanswer",
            "security_answer",
            "securityquestion",
            "security_question",
            
            // Biometric Data
            "fingerprint",
            "finger_print",
            "retinascan",
            "retina_scan",
            "facialrecognition",
            "facial_recognition",
            "biometric",
            "biometricdata",
            "biometric_data",
            
            // Government & Legal
            "alienregistration",
            "alien_registration",
            "visa",
            "visanumber",
            "visa_number",
            "citizenship",
            "nationality",
            "immigrationstatus",
            "immigration_status",
            
            // Vehicle Information
            "vehiclevin",
            "vehicle_vin",
            "vin",
            "licenseplate",
            "license_plate",
            "registration",
            
            // Payment Processor Tokens
            "stripetoken",
            "stripe_token",
            "paypaltoken",
            "paypal_token",
            "braintreetoken",
            "braintree_token",
            "squaretoken",
            "square_token",
            
            // API & Integration Secrets
            "webhooksecret",
            "webhook_secret",
            "callbacktoken",
            "callback_token",
            "integrationsecret",
            "integration_secret",
            
            // Encryption & Keys
            "privatekey",
            "private_key",
            "publickey",
            "public_key",
            "encryptionkey",
            "encryption_key",
            "decryptionkey",
            "decryption_key",
            "pgpkey",
            "pgp_key",
            "gpgkey",
            "gpg_key",
            
            // Two-Factor Authentication
            "2facode",
            "2fa_code",
            "totp",
            "totpcode",
            "totp_code",
            "verificationcode",
            "verification_code",
            "smscode",
            "sms_code",
            
            // Password Reset
            "reset_token",
            "resettoken",
            "passwordresettoken",
            "password_reset_token",
            
            // Account Recovery
            "recoverycode",
            "recovery_code",
            "backupcode",
            "backup_code",
            
            // Session & Browser
            "browserfingerprint",
            "browser_fingerprint",
            "useragent",
            "user_agent",
            
            // Location Data
            "gps",
            "latitude",
            "longitude",
            "coordinates",
            "location",
            
            // Health & Genetic
            "genetic",
            "dna",
            "bloodtype",
            "blood_type",
            "medicalcondition",
            "medical_condition",
            
            // Legal & Compliance
            "attorneyname",
            "attorney_name",
            "conservator",
            "guardian",
            "trustee",
            
            // Insurance
            "policy_number",
            "policynumber",
            "claim_number",
            "claimnumber",
            "group_number",
            "groupnumber"
        };
        #endregion

    }
}
