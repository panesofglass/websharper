// $begin{copyright}
//
// This file is part of WebSharper
//
// Copyright (c) 2008-2015 IntelliFactory
//
// Licensed under the Apache License, Version 2.0 (the "License"); you
// may not use this file except in compliance with the License.  You may
// obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
// implied.  See the License for the specific language governing
// permissions and limitations under the License.
//
// $end{copyright}

namespace WebSharper.InterfaceGenerator

open System
open System.CodeDom
open System.IO
open Microsoft.CSharp

module internal Util =

    /// Quotes a string, returning a string literal.
    let Quote (text: string) =
        use writer = new StringWriter()
        use provider = new CSharpCodeProvider()
        let expr = new CodePrimitiveExpression(text)
        provider.GenerateCodeFromExpression(expr, writer, null)
        writer.ToString()

