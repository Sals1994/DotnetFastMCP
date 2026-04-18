# Multi-Factor Authentication (MFA) Support

**DotnetFastMCP** enables granular security policies for your tools, including the enforcement of Multi-Factor Authentication (MFA) for sensitive operations.

## 🛡️ Overview

The MFA Support feature allows you to decorate specific tools with `[AuthorizeMcpTool(RequireMfa = true)]`. When this attribute is present, the server validates that the authenticated user's token contains the **Authentication Methods References (amr)** claim with the value `mfa`.

This ensures that critical tools (e.g., specific to payments, infrastructure changes, PII access) are only invoked by users who have authenticated with a second factor.

---

## 🚀 Getting Started

### 1. Enable MFA on a Tool

Simply strictly require MFA by setting the property on the attribute.

```csharp
public static class SensitiveTools
{
    [McpTool("transfer_funds")]
    [AuthorizeMcpTool(RequireMfa = true)]
    public static string TransferFunds(string account, decimal amount)
    {
        // This code runs ONLY if the user has MFA
        return $"Transferred {amount} to {account}";
    }
}
```

### 2. Provider Configuration

Ensure your OAuth provider (Azure AD, Auth0, Okta, etc.) is configured to emit the `amr` claim.

-   **Azure AD**: Use Conditional Access policies to enforce MFA. The token will include `amr: ["mfa"]`.
-   **Auth0**: Enable MFA rules. The ID token will include `amr: mfa`.

### 3. Client Behavior

If a client attempts to call an MFA-protected tool without an MFA claim:
1.  The server checks the claim.
2.  The server returns a standard **JSON-RPC Error** (Unauthorized/Forbidden).
3.  The tool logic is **not executed**.

---

## 🧪 Verification & Testing

Since MFA relies on claims from an external provider, verifying it locally usually involves simulating the user context.

### Manual Verification Harness

You can create a small test harness to verify the enforcement logic programmatically.

```csharp
// Program.cs in a Test Project
var server = new FastMCPServer("TestServer");
// ... register tool ...

// Create a user WITHOUT MFA
var userNoMfa = new ClaimsPrincipal(new ClaimsIdentity(new[] 
{ 
    new Claim("sub", "user1") 
}));

// Create a user WITH MFA
var userMfa = new ClaimsPrincipal(new ClaimsIdentity(new[] 
{ 
    new Claim("sub", "user1"),
    new Claim("amr", "mfa") 
}));

// Verify (using McpRequestHandler)
// ...
```

### Running the Example Verification

The framework includes an example project `examples/MfaVerification` that demonstrates this verification.

```bash
dotnet run --project examples/MfaVerification/MfaVerification.csproj
```

**Expected Output:**
```
Test A: No User (Should Fail)       -> [PASS] Request denied as expected.
Test B: User, No Claims (Should Fail)-> [PASS] Request denied as expected.
Test C: User, Wrong AMR (Should Fail)-> [PASS] Request denied as expected.
Test D: User, MFA Claim (Should Succeed)-> [PASS] Request succeeded as expected.
```

## 🏗️ Technical Details

-   **Attribute**: `FastMCP.Attributes.AuthorizeMcpTool.RequireMfa`
-   **Claim Checked**: `amr` (RFC 8176)
-   **Value Expected**: `mfa`

This implementation follows standard OAuth 2.0 and OIDC practices for authentication strength validation.
