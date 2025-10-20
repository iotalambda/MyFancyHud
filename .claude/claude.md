# Project Instructions

## Build Verification

Always after substantial code changes run `dotnet build` to check if there are errors or warnings. If yes, fix them and build until no more errors or warnings.

## Transparency and Windows Forms

When working with transparency in Windows Forms:

1. **TransparencyKey with Color.Magenta**: This approach makes the specified color (e.g., Magenta) completely transparent, but it DOES NOT support alpha blending or semi-transparency. If you draw something on a Magenta background with TransparencyKey set, it will be visible immediately at full opacity, not gradually faded.

2. **For fade effects with alpha blending**: Use a layered window (`WS_EX_LAYERED`) and control the entire form's `Opacity` property (0.0 to 1.0). The form should have a solid background color (like White or Black), and the OS will handle the alpha blending correctly.

3. **Rule of thumb**: If you need gradual opacity changes or semi-transparent effects, use layered windows with the `Opacity` property, NOT `TransparencyKey`. `TransparencyKey` is only for making specific colors fully transparent (click-through backgrounds).
