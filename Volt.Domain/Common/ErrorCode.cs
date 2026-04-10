using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Volt.Domain.Common
{
    public static class ErrorCode
    {
        public const string SERVER_ERROR     = "SERVER_ERROR";
        public const string VALIDATION_ERROR = "VALIDATION_ERROR";
        public const string INVALID_USERNAME = "INVALID_USERNAME";
        public const string INVALID_PASSWORD = "INVALID_PASSWORD";
        public const string ADMIN_NOT_FOUND = "Admin not found";


        public const string ABOUT_NOT_FOUND = "About not found";
        public const string ABOUT_LANGUAGE_DUPLICATE = "Duplicate language exists for about";
        public const string ABOUT_IMAGE_NOT_FOUND = "About image not found";
        public const string INVALID_ABOUT_REQUEST = "Invalid about request";
        public const string INVALID_ABOUT_REORDER_REQUEST = "Invalid about reorder request";

        public const string FILE_REQUIRED = "File is required";
        public const string FILE_URL_REQUIRED = "File url is required";

        public const string STEP_NOT_FOUND = "Step not found";
        public const string STEP_LANGUAGE_DUPLICATE = "Duplicate language exists for step";
        public const string INVALID_STEP_REQUEST = "Invalid step request";
        public const string INVALID_STEP_REORDER_REQUEST = "Invalid step reorder request";
        public const string STEP_IMAGE_REQUIRED = "Step image is required";

        public const string SERVICE_NOT_FOUND = "Service not found";
        public const string SERVICE_LANGUAGE_DUPLICATE = "Duplicate language exists for service";
        public const string INVALID_SERVICE_REQUEST = "Invalid service request";
        public const string INVALID_SERVICE_REORDER_REQUEST = "Invalid service reorder request";
        public const string SERVICE_IMAGE_REQUIRED = "Service image is required";

        public const string APPLICATION_TYPE_NOT_FOUND = "Application type not found";
        public const string APPLICATION_TYPE_LANGUAGE_DUPLICATE = "Duplicate language exists for application type";
        public const string INVALID_APPLICATION_TYPE_REQUEST = "Invalid application type request";

        public const string CONTACT_INFO_NOT_FOUND = "Contact info not found";
        public const string CONTACT_LANGUAGE_DUPLICATE = "Duplicate language exists for contact info";
        public const string INVALID_CONTACT_INFO_REQUEST = "Invalid contact info request";
        public const string CONTACT_CHANNEL_REQUIRED = "At least one phone number or email is required";
    }
}
