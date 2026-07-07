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
        public const string CUSTOMER_NOT_FOUND = "Customer not found";
        public const string CUSTOMER_ALREADY_EXISTS = "Customer already exists";
        public const string INVALID_CUSTOMER_REQUEST = "Invalid customer request";
        public const string INVALID_EXTERNAL_AUTH_TOKEN = "Invalid external auth token";
        public const string INVALID_PASSKEY_CHALLENGE = "Invalid passkey challenge";
        public const string PASSKEY_NOT_FOUND = "Passkey not found";


        public const string ABOUT_NOT_FOUND = "About not found";
        public const string ABOUT_LANGUAGE_DUPLICATE = "Duplicate language exists for about";
        public const string ABOUT_IMAGE_NOT_FOUND = "About image not found";
        public const string INVALID_ABOUT_REQUEST = "Invalid about request";
        public const string INVALID_ABOUT_REORDER_REQUEST = "Invalid about reorder request";

        public const string FILE_REQUIRED = "File is required";
        public const string FILE_URL_REQUIRED = "File url is required";
        public const string INVALID_FILE_EXTENSION = "Invalid file extension";

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

        public const string PARTNERSHIP_TYPE_NOT_FOUND = "Partnership type not found";
        public const string PARTNERSHIP_TYPE_LANGUAGE_DUPLICATE = "Duplicate language exists for partnership type";
        public const string PARTNERSHIP_TYPE_ALL_LANGUAGES_REQUIRED = "All 4 languages are required for partnership type";
        public const string INVALID_PARTNERSHIP_TYPE_REQUEST = "Invalid partnership type request";

        public const string PARTNERSHIP_REQUEST_NOT_FOUND = "Partnership request not found";
        public const string PARTNERSHIP_REQUEST_STATUS_UNCHANGED = "Partnership request status is already set to this value";

        public const string CONTACT_INFO_NOT_FOUND = "Contact info not found";
        public const string CONTACT_LANGUAGE_DUPLICATE = "Duplicate language exists for contact info";
        public const string INVALID_CONTACT_INFO_REQUEST = "Invalid contact info request";
        public const string CONTACT_CHANNEL_REQUIRED = "At least one phone number or email is required";

        public const string SERVICE_REQUEST_NOT_FOUND = "Service request not found";
        public const string INVALID_SERVICE_REQUEST_REQUEST = "Invalid service request request";
        public const string INVALID_STATUS_TRANSITION = "Invalid status transition";
        public const string INVALID_STATUS_VALUE = "Status must be between 1 and 3";

        public const string CONTACT_REQUST_NOT_FOUND = "Contact requst not found";
        public const string INVALID_CONTACT_REQUST_REQUEST = "Invalid contact requst request";

        public const string BLOG_NOT_FOUND = "Blog not found";
        public const string BLOG_LANGUAGE_DUPLICATE = "Duplicate language exists for blog";
        public const string INVALID_BLOG_REQUEST = "Invalid blog request";

        public const string NEWS_POST_NOT_FOUND = "News post not found";
        public const string NEWS_POST_LANGUAGE_DUPLICATE = "Duplicate language exists for news post";
        public const string INVALID_NEWS_POST_REQUEST = "Invalid news post request";

        public const string PROJECT_NOT_FOUND = "Project not found";
        public const string PROJECT_LANGUAGE_DUPLICATE = "Duplicate language exists for project";
        public const string PROJECT_IMAGE_NOT_FOUND = "Project image not found";
        public const string INVALID_PROJECT_REQUEST = "Invalid project request";
        public const string INVALID_PROJECT_REORDER_REQUEST = "Invalid project reorder request";

        public const string PRODUCT_CATEGORY_NOT_FOUND = "Product category not found";
        public const string PRODUCT_CATEGORY_LANGUAGE_DUPLICATE = "Duplicate language exists for product category";
        public const string INVALID_PRODUCT_CATEGORY_REQUEST = "Invalid product category request";


        public const string PRODUCT_SUBCATEGORY_NOT_FOUND = "Product subcategory not found";
        public const string PRODUCT_SUBCATEGORY_LANGUAGE_DUPLICATE = "Duplicate language exists for product subcategory";
        public const string INVALID_PRODUCT_SUBCATEGORY_REQUEST = "Invalid product subcategory request";

        public const string PROMOTION_NOT_FOUND = "Promotion not found";
        public const string PROMOTION_LANGUAGE_DUPLICATE = "Duplicate language exists for promotion";
        public const string INVALID_PROMOTION_REQUEST = "Invalid promotion request";

        public const string PRODUCT_BRAND_NOT_FOUND = "Product brand not found";
        public const string INVALID_PRODUCT_BRAND_REQUEST = "Invalid product brand request";

        public const string PRODUCT_TECHNOLOGY_NOT_FOUND = "Product technology not found";
        public const string INVALID_PRODUCT_TECHNOLOGY_REQUEST = "Invalid product technology request";
        public const string PRODUCT_NOT_FOUND = "Product not found";
        public const string INVALID_PRODUCT_REQUEST = "Invalid product request";
        public const string PRODUCT_PROMOTION_CONFLICT = "Promotion is already assigned to another product";
        public const string PRODUCT_DESCRIPTION_LANGUAGE_DUPLICATE = "Duplicate language exists for product description";

        public const string ORDER_NOT_FOUND = "Order not found";
        public const string INVALID_ORDER_REQUEST = "Invalid order request";
        public const string INVALID_ORDER_STATUS = "Invalid order status";
        public const string INVALID_PAYMENT_STATUS = "Invalid payment status";

    }
}
