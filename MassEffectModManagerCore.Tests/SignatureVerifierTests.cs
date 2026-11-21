using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ME3TweaksCore.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ME3TweaksModManager.Tests
{
    /// <summary>
    /// Subjects are built from the verifier's configured fields.
    /// </summary>
    [TestClass]
    public class SignatureVerifierTests
    {
        private static IEnumerable<SignerSubjectField> Required => ME3TweaksSignatureVerifier.RequiredSubjectFields;

        [TestMethod]
        public void AcceptsRequiredSubject()
        {
            using var cert = MakeCert(Subject(Required));
            Assert.IsTrue(ME3TweaksSignatureVerifier.IsME3TweaksCertificate(cert, out var reason), reason);
        }

        [TestMethod]
        public void IgnoresExtraSubjectFields()
        {
            // Past renewals added and dropped locality and postal code
            using var cert = MakeCert(Subject(Required) + ", L=Somewhere, OID.2.5.4.17=00000");
            Assert.IsTrue(ME3TweaksSignatureVerifier.IsME3TweaksCertificate(cert, out var reason), reason);
        }

        [TestMethod]
        public void RejectsWhenAnyRequiredFieldDiffers()
        {
            foreach (var field in Required)
            {
                var changed = Required.Select(f => f == field ? f with { Value = f.Value + "x" } : f);
                using var cert = MakeCert(Subject(changed));
                Assert.IsFalse(ME3TweaksSignatureVerifier.IsME3TweaksCertificate(cert, out _), $"Accepted with different {field.Name}");
            }
        }

        [TestMethod]
        public void RejectsWhenAnyRequiredFieldMissing()
        {
            foreach (var field in Required)
            {
                using var cert = MakeCert(Subject(Required.Where(f => f != field)));
                Assert.IsFalse(ME3TweaksSignatureVerifier.IsME3TweaksCertificate(cert, out _), $"Accepted without {field.Name}");
            }
        }

        [TestMethod]
        public void RejectsDifferentCase()
        {
            var changed = Required.Select(f => f with { Value = f.Value.ToLowerInvariant() == f.Value ? f.Value.ToUpperInvariant() : f.Value.ToLowerInvariant() });
            using var cert = MakeCert(Subject(changed));
            Assert.IsFalse(ME3TweaksSignatureVerifier.IsME3TweaksCertificate(cert, out _));
        }

        [TestMethod]
        public void RejectsDuplicateCommonName()
        {
            using var cert = MakeCert(Subject(Required) + ", CN=Someone Else");
            Assert.IsFalse(ME3TweaksSignatureVerifier.IsME3TweaksCertificate(cert, out _));
        }

        [TestMethod]
        public void RejectsCertWithoutCodeSigningUsage()
        {
            using var cert = MakeCert(Subject(Required), codeSigning: false);
            Assert.IsFalse(ME3TweaksSignatureVerifier.IsME3TweaksCertificate(cert, out _));
        }

        [TestMethod]
        public void RejectsUnsignedFile()
        {
            var result = ME3TweaksSignatureVerifier.CheckFile(typeof(SignatureVerifierTests).Assembly.Location, out _);
            Assert.AreEqual(ME3TweaksSignatureCheckResult.InvalidSignature, result);
        }

        /// <summary>
        /// Opt-in: set M3_SIGNED_BUILDS_DIR to a folder of real signed builds (exes) to check them all pass end to end.
        /// </summary>
        [TestMethod]
        public void AcceptsRealSignedBuilds()
        {
            var dir = Environment.GetEnvironmentVariable("M3_SIGNED_BUILDS_DIR");
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            {
                Assert.Inconclusive("Set M3_SIGNED_BUILDS_DIR to a folder of signed ME3Tweaks builds to run this test.");
            }

            var files = Directory.GetFiles(dir, "*.exe");
            if (files.Length == 0)
            {
                Assert.Inconclusive($"No exes found in {dir}");
            }

            foreach (var file in files)
            {
                var result = ME3TweaksSignatureVerifier.CheckFile(file, out var reason);
                Assert.AreEqual(ME3TweaksSignatureCheckResult.Trusted, result, $"{Path.GetFileName(file)}: {reason}");
            }
        }

        private static string Subject(IEnumerable<SignerSubjectField> fields) =>
            string.Join(", ", fields.Select(f => $"OID.{f.Oid}=\"{f.Value}\""));

        private static X509Certificate2 MakeCert(string subject, bool codeSigning = true)
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            if (codeSigning)
            {
                request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.3") }, false));
            }
            return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        }
    }
}
