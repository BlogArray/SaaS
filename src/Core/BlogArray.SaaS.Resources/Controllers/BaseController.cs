using BlogArray.SaaS.Mvc.Services;
using BlogArray.SaaS.Mvc.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Net;
using System.Security.Claims;

namespace BlogArray.SaaS.Resources.Controllers;

public class BaseController : Controller
{
    public string? LoggedInUserID => User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

    public string? LoggedInUserEmail => User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;

    protected IActionResult JsonError(dynamic message)
    {
        ReturnResult returnResult = new()
        {
            Status = false,
            Message = Converter.ToString(message)
        };
        return BadRequest(returnResult);
    }

    protected IActionResult ModelStateError(ModelStateDictionary ModelState)
    {
        IEnumerable<string> errors = ModelState.Values.SelectMany(state => state.Errors).Select(error => error.ErrorMessage);
        ReturnResult returnResult = new()
        {
            Status = false,
            Message = string.Join(". ", errors)
        };
        return BadRequest(returnResult);
    }

    protected IActionResult JsonSuccess(dynamic message)
    {
        ReturnResult returnResult = new()
        {
            Status = true,
            Message = Converter.ToString(message)
        };

        return Ok(returnResult);
    }

    protected static IActionResult Exception(HttpStatusCode statusCode)
    {
        throw new Exception(statusCode.ToString());
    }

    protected void AddErrorMessage(string error)
    {
        AddMessage("danger", error);
    }

    protected void AddSuccessMessage(string error)
    {
        AddMessage("success", error);
    }

    private void AddMessage(string type, string error)
    {
        TempData["AlertMessage"] = error;
        TempData["AlertType"] = type;
    }

    protected IActionResult IdentityErrorResult(IEnumerable<IdentityError> identityErrors)
    {
        string errors = string.Join(". ", identityErrors.Select(e => e.Description));

        return JsonError(errors);
    }

}
