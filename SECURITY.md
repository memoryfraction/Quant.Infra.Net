# Security Policy

## Reporting a Vulnerability

We take the security of **Quant.Infra.Net** seriously. If you believe you have found a security vulnerability, please report it to us responsibly.

**Do not open a public issue for security vulnerabilities.**

Please report by:

- Email: `security@quant-infra.net`
- or via the [GitHub Private Vulnerability Reporting](https://docs.github.com/code-security/responsible-disclosure-for-github-advisories/about-github-private-vulnerability-reporting) feature from the repository's **Security** tab.

We aim to acknowledge all reports within **72 hours** and to publish a fix within **14 days** of confirmation where possible.

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 1.5.x   | :white_check_mark: |
| < 1.0   | :x:                |

## Scope

- .NET library source (`src/`)
- NuGet package `Quant.Infra.Net`

Out of scope:
- Third-party broker/exchange APIs and their servers
- Documentation sites and static page hosting

## Responsible Disclosure Guidelines

1. Provide steps to reproduce and affected versions.
2. Allow us reasonable time to investigate and issue a fix before public disclosure.
3. Do not exploit or expose the vulnerability to other users.
