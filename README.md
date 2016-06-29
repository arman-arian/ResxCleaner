# ResxCleaner
ResxCleaner helps you find and eliminate unused resources in your app.
It will search in all files in the specified project folder and look for usages of strings in a RESX file you specify. Matches are found from a list of templates: Typically this would be a template for a usage in code and a template for usage in XAML. It then shows a list of all the resources that had no matches. You can then look them over, exclude ones that you want to keep, then delete the rest with one click.

Sometimes resource keys are created dynamically, for example by combining a prefix with an enum value. You can specify a list of prefixes that will never be listed as "unused".
