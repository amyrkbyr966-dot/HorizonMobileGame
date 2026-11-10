# CI setup — Unity license (UNITY_LICENSE secret)

The Linux build workflow (`.github/workflows/build.yml`) cannot run
until the repo has a `UNITY_LICENSE` secret. The secret holds the
content of a Unity `.ulf` license file. Follow these steps exactly.

## 1. Add your Unity ID as secrets

Repo page -> Settings -> Secrets and variables -> Actions -> New
repository secret. Add these two:

- `UNITY_EMAIL` — the email of your Unity ID.
- `UNITY_PASSWORD` — the password of your Unity ID.

## 2. Run the workflow once to get the activation file

Repo page -> Actions -> "Linux build (TextureFix) + smoke test" ->
Run workflow. The run fails at the activation step. That is expected.

When it finishes, open the failed run and download the
`unity-activation-file` artifact. It contains a `.alf` file (the
manual activation request for the GitHub runner machine).

## 3. Turn the .alf into a .ulf on Unity's site

- Open https://license.unity3d.com/manual in your browser.
- Upload the `.alf` file, complete the form, download the `.ulf`
  license file it returns.

## 4. Add the .ulf as the UNITY_LICENSE secret

Repo page -> Settings -> Secrets and variables -> Actions -> New
repository secret:

- Name: `UNITY_LICENSE`
- Value: the whole content of the `.ulf` file (open it in a text
  editor, copy everything, paste it as the secret value).

## 5. Re-run the workflow

Actions -> the workflow -> Run workflow. It now activates with
`UNITY_LICENSE` and the build proceeds. A successful run uploads
the `linux-smoke-<sha>` artifact with menu screenshots and the
Player.log.

## What is NOT uploaded

The built Linux binary is about 1.9 GB. GitHub's free artifact
storage is 500 MB, so the binary is never uploaded. Only the
screenshots and logs are kept. The binary is discarded when the
runner finishes.

## Texture cap note

The project currently has a temporary 128px texture cap so the
Linux build fits in memory. Facades look softer in this verification
build. The 256px design target is restored once a build fits.
