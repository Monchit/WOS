/*! jQuery Validation Plugin - v1.00.0 - 01/10/2013
* Copyleft (L) 2013 Peetinun Kaweekul */

(function($) {

    $.validate = function (options) {
        if (options != null && options.form != null) {
            var elems = $('#' + options.form).find("input, textarea, select");
        }
        else {
            var elems = $("input, textarea, select");
        }

        var isValid = true;
        var tmpStatus = true;

        elems.each(function () {
            var validator = $(this).attr("data-validate");
            var value = $(this).val() != '' ? $(this).val() : $(this).text();

            if (validator != null) {
                // Check required
                if (tmpStatus && validator.indexOf("required") != -1) {
                    if (($(this).prop("tagName") == "INPUT" && value == '') ||
                        ((($(this).prop("tagName") == "SELECT" && $(this).attr("multiple") != "multiple") ||
                        $(this).prop("tagName") == "TEXTAREA") && (value == 0 || value == '')) ||
                        ($(this).prop("tagName") == "SELECT" && $(this).attr("multiple") == "multiple" && $(this).find("option").length == 0)) {
                        $(this).addClass("error");
                        isValid = false;
                        tmpStatus = false;
                    }
                    else {
                        $(this).removeClass("error");
                        $(this).addClass("valid");
                    }
                }

                // Check integer
                if (tmpStatus && validator.indexOf("integer") != -1) {
                    if (value == '' || !(value.replace(/[0-9]/g, '') === '')) {
                        $(this).addClass("error");
                        isValid = false;
                        tmpStatus = false;
                    }
                    else {
                        $(this).removeClass("error");
                        $(this).addClass("valid");
                    }
                }

                // Check float
                if (tmpStatus && validator.indexOf("float") != -1) {
                    var decimalSeparator = '.';
                    if (value == '' || !(value.match(new RegExp('^([0-9]+)\\' + decimalSeparator + '([0-9]+)$')) !== null)) {
                        $(this).addClass("error");
                        isValid = false;
                        tmpStatus = false;
                    }
                    else {
                        $(this).removeClass("error");
                        $(this).addClass("valid");
                    }
                }

                // Check number
                if (tmpStatus && validator.indexOf("number") != -1) {
                    if (value == '' || !(value.match(new RegExp('^([0-9]+)(\\' + decimalSeparator + '([0-9]+))?')) !== null)) {
                        $(this).addClass("error");
                        isValid = false;
                        tmpStatus = false;
                    }
                    else {
                        $(this).removeClass("error");
                        $(this).addClass("valid");
                    }
                }

                // Check length
                if (tmpStatus && validator.indexOf("length") != -1) {
                    var length = $(this).attr("data-validate-length").split('-');
                    if (value == '' || (length.length == 2 && ($.trim(length[0]) !== parseInt($.trim(length[0])) || $.trim(length[1]) !== parseInt($.trim(length[1]))) && !(value.length >= parseInt($.trim(length[0])) && value.length <= parseInt($.trim(length[1]))))) {
                        $(this).addClass("error");
                        isValid = false;
                        tmpStatus = false;
                    }
                    else {
                        $(this).removeClass("error");
                        $(this).addClass("valid");
                    }
                }

                // Check date
                if (tmpStatus && validator.indexOf("date") != -1) {
                    var defaultFormat = (options != null && options.dateFormat != null) ? options.dateFormat : 'dd-mm-yyyy';
                    var dateFormat = $(this).attr("data-validate-format") == '' ? defaultFormat : $(this).attr("data-validate-format");
                    if (value == '' || value.parseDate(date, dateFormat) === false) {
                        $(this).addClass("error");
                        isValid = false;
                        tmpStatus = false;
                    }
                    else {
                        $(this).removeClass("error");
                        $(this).addClass("valid");
                    }
                }

                // Check File
                if (tmpStatus && validator.indexOf("file") != -1) {
                    //var extension = $(this).attr("data-validate-extension").split(',');
                    if (value == '' || value.substr(value.lastIndexOf('.') + 1).toUpperCase() != "PDF"){
                        $(this).addClass("error");
                        isValid = false;
                        tmpStatus = false;
                    } else {
                        $(this).removeClass("error");
                        $(this).addClass("valid");
                    }
                }
            }
            tmpStatus = true;
        });

        return isValid;
    };

}(jQuery));