/**
 * @license Copyright (c) 2003-2020, CKSource - Frederico Knabben. All rights reserved.
 * For licensing, see https://ckeditor.com/legal/ckeditor-oss-license
 */

CKEDITOR.editorConfig = function (config) {
    /*ȥ��ͼƬԤ���������*/
    config.image_previewText = ' ';
    // �������ԣ�Ĭ��Ϊ 'en'
    config.language = 'zh-cn';
    // ���ÿ��
    config.height = 500;
    //���س�������߼�ѡ��
    config.removeDialogTabs = 'image:advanced;image:Link';
    config.toolbarGroups = [
        { name: 'clipboard', groups: ['clipboard', 'undo'] },
        { name: 'editing', groups: ['find', 'selection', 'spellchecker', 'editing'] },
        { name: 'forms', groups: ['forms'] },
        { name: 'basicstyles', groups: ['basicstyles', 'cleanup'] },
        { name: 'paragraph', groups: ['list', 'indent', 'blocks', 'align', 'bidi', 'paragraph'] },
        { name: 'links', groups: ['links'] },
        { name: 'insert', groups: ['insert'] },
        { name: 'styles', groups: ['styles'] },
        { name: 'colors', groups: ['colors'] },
        { name: 'document', groups: ['document', 'doctools', 'mode'] },
        { name: 'tools', groups: ['tools'] },
        { name: 'others', groups: ['others'] },
        { name: 'about', groups: ['about'] }
    ];
    //�Ƴ��İ�ť
    config.removeButtons = 'Templates,Print,Find,Replace,SelectAll,Scayt,Checkbox,Form,Radio,TextField,Textarea,Select,Button,ImageButton,HiddenField,CreateDiv,Blockquote,BidiLtr,BidiRtl,Flash,PageBreak,Iframe,About,ShowBlocks,Smiley,SpecialChar,HorizontalRule,CopyFormatting,RemoveFormat';

    //�ϴ�ͼƬ�����õ��Ľӿ�
    config.filebrowserImageUploadUrl = "/Api/Tools/CkEditorUploadFiles";
    config.filebrowserUploadUrl = "/Api/Tools/CkEditorUploadFiles";

    // ʹ�ϴ�ͼƬ�������ֶ�Ӧ�ġ��ϴ���tab��ǩ
    config.removeDialogTabs = 'image:advanced;link:advanced';

    //ճ��ͼƬʱ�õõ�
    config.extraPlugins = 'uploadimage';
    config.uploadUrl = '/Api/Tools/CkEditorUploadFiles';

    //��Ƶ���
    config.extraPlugins = 'html5video,widget';

    // ������������'Basic'��ȫ��'Full'���Զ��壩plugins/toolbar/plugin.js
    config.baseFloatZIndex = 99999999;
};
