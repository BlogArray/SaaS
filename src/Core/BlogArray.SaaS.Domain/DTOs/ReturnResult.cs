//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

namespace BlogArray.SaaS.Domain.DTOs;

public class ReturnResult<T> : ReturnResult
{
    public T Result { get; set; }
}

public class ReturnResult
{
    public bool Status { get; set; } = false;
    public string Message { get; set; }
}

