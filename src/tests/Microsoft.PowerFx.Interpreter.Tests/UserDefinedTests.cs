﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using Microsoft.PowerFx.Syntax;
using Microsoft.PowerFx.Types;
using Xunit;

namespace Microsoft.PowerFx.Interpreter.Tests
{
    public class UserDefinedTests
    {
        [Theory]
        [InlineData("x=1;y=2;z=x+y;", "Float(Abs(-(x+y+z)))", 6d)]
        [InlineData("x=1;y=2;Foo(x: Number): Number = Abs(x);", "Foo(-(y*y)+x)", 3d)]
        [InlineData("myvar=Weekday(Date(2024,2,2)) > 1 And false;Bar(x: Number): Number = x + x;", "Bar(1) + myvar", 2d)]
        public void NamedFormulaEntryTest(string script, string expression, double expected)
        {
            var engine = new RecalcEngine();

            engine.AddUserDefinitions(script);

            var check = engine.Check(expression);
            Assert.True(check.IsSuccess);

            var result = (NumberValue)check.GetEvaluator().Eval();
            Assert.Equal(expected, result.Value);
        }

        [Theory]
        [InlineData("test2(b: Boolean): Boolean = { Set(a, b); };")]
        [InlineData("test2(b: Boolean): Boolean = { Set(a, b); Collect(abc, { bcd: 1 }) };")]
        [InlineData("test2(b: Boolean): Boolean = { Set(a, b); num = 3; Collect(abc, { bcd: 1 }) };")]
        public void UserDefinedFunctions(string script)
        {
            var result = UserDefinitions.Process(script, null);

            Assert.False(result.HasErrors);
        }
    }
}
