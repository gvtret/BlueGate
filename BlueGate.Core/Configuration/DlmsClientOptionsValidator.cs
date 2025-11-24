using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

namespace BlueGate.Core.Configuration
{
    public class DlmsClientOptionsValidator : IValidateOptions<DlmsClientOptions>
    {
        public ValidateOptionsResult Validate(string name, DlmsClientOptions options)
        {
            var context = new ValidationContext(options);
            var results = new System.Collections.Generic.List<ValidationResult>();
            if (!Validator.TryValidateObject(options, context, results, true))
            {
                var errors = string.Join(", ", results);
                return ValidateOptionsResult.Fail(errors);
            }

            if (options.SecuritySuite != Gurux.DLMS.SecuritySuite.None)
            {
                if (string.IsNullOrWhiteSpace(options.BlockCipherKey) || !IsHex(options.BlockCipherKey))
                {
                    results.Add(new ValidationResult("BlockCipherKey must be a valid hex string when a security suite is enabled."));
                }

                if (string.IsNullOrWhiteSpace(options.AuthenticationKey) || !IsHex(options.AuthenticationKey))
                {
                    results.Add(new ValidationResult("AuthenticationKey must be a valid hex string when a security suite is enabled."));
                }

                if (string.IsNullOrWhiteSpace(options.SystemTitle) || !IsHex(options.SystemTitle))
                {
                    results.Add(new ValidationResult("SystemTitle must be a valid hex string when a security suite is enabled."));
                }

                if (options.Security != Gurux.DLMS.Security.AuthenticationEncryption)
                {
                    results.Add(new ValidationResult("Security must be AuthenticationEncryption when a security suite is enabled."));
                }
            }

            if (results.Count > 0)
            {
                var errors = string.Join(", ", results);
                return ValidateOptionsResult.Fail(errors);
            }

            return ValidateOptionsResult.Success;
        }

        private bool IsHex(string value)
        {
            return value.Length % 2 == 0 && System.Text.RegularExpressions.Regex.IsMatch(value, @"^[0-9a-fA-F]+$");
        }
    }
}
