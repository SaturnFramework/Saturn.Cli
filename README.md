# Saturn.Dotnet

`dotnet` CLI tool for [Saturn](https://github.com/SaturnFramework/Saturn) providing code generation and scaffolding.

To install the tool globally run `dotnet tool install saturn.cli -g` in your terminal.

## Commands

`dotnet saturn` supports following commands:

* `gen NAME NAMES COLUMN:TYPE COLUMN:TYPE COLUMN:TYPE ...` - creates model, database layer, views and controller returning HTML views
* `gen.json NAME NAMES COLUMN:TYPE COLUMN:TYPE COLUMN:TYPE ...` - creates model, database layer and JSON API controller
* `gen.model NAME NAMES COLUMN:TYPE COLUMN:TYPE COLUMN:TYPE ...` - creates model and database layer
* `migration` - runs all migration scripts for the databse

## Types

Generator supports following types:

* `string`
* `int`
* `float`
* `double`
* `decimal`
* `guid`
* `datetime`
* `bool`

## How to build

1. Make sure you have installed version of .Net SDK defined in `global.json`
2. Run `dotnet tool restore` to restore all necessary tools
3. Run `dotnet fake build` to build project

## How to contribute

*Imposter syndrome disclaimer*: I want your help. No really, I do.

There might be a little voice inside that tells you you're not ready; that you need to do one more tutorial, or learn another framework, or write a few more blog posts before you can help me with this project.

I assure you, that's not the case.

This project has some clear Contribution Guidelines and expectations that you can [read here](https://github.com/SaturnFramework/Saturn.Dotnet/blob/master/CONTRIBUTING.md).

The contribution guidelines outline the process that you'll need to follow to get a patch merged. By making expectations and process explicit, I hope it will make it easier for you to contribute.

And you don't just have to write code. You can help out by writing documentation, tests, or even by giving feedback about this work. (And yes, that includes giving feedback about the contribution guidelines.)

Thank you for contributing!


## Contributing and copyright

The project is hosted on [GitHub](https://github.com/SaturnFramework/Saturn.Dotnet) where you can [report issues](https://github.com/SaturnFramework/Saturn.Dotnet/issues), fork
the project and submit pull requests.

The library is available under [MIT license](https://github.com/SaturnFramework/Saturn.Dotnet/blob/master/LICENSE.md), which allows modification and redistribution for both commercial and non-commercial purposes.

Please note that this project is released with a [Contributor Code of Conduct](CODE_OF_CONDUCT.md). By participating in this project you agree to abide by its terms.
