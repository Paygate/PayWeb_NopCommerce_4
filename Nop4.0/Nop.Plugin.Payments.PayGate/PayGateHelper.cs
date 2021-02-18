using System.Text;
using Nop.Core.Domain.Payments;

namespace Nop.Plugin.Payments.PayGate
{
    /// <summary>
    /// Represents pagate helper
    /// </summary>
    public class PayGateHelper
    {
        /// <summary>
        /// Gets a payment status
        /// </summary>
        /// <param name="paymentStatus">PayGate payment status</param>
        /// <param name="pendingReason">PayGate pending reason</param>
        /// <returns>Payment status</returns>
        public static PaymentStatus GetPaymentStatus(string paymentStatus, string pendingReason)
        {
            var result = PaymentStatus.Pending;

            return result;
        }

        public string CalculateMD5Hash(string input)
        {

            System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create();

            byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);

            byte[] hash = md5.ComputeHash(inputBytes);

            // step 2, convert byte array to hex string

            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("X2"));
            }
            return sb.ToString();

        }
    }
}