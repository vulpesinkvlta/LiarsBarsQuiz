mergeInto(LibraryManager.library, {
    LiarsBarCopyRoomCode: function (pointer) {
        var code = UTF8ToString(pointer);
        function copySelection() {
            var previous = document.activeElement;
            var field = document.createElement("textarea");
            field.value = code;
            field.style.position = "fixed";
            field.style.opacity = "0";
            document.body.appendChild(field);
            field.select();
            try {
                if (!document.execCommand("copy")) window.prompt("Код комнаты", code);
            } finally {
                field.remove();
                if (previous) previous.focus();
            }
        }
        if (navigator.clipboard && window.isSecureContext)
            navigator.clipboard.writeText(code).catch(copySelection);
        else
            copySelection();
    }
});
