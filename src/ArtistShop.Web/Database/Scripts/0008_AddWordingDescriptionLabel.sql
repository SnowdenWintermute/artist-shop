-- What the website calls a work's description, which has no plural. NULL is the default, so the
-- existing row follows it
ALTER TABLE wording
ADD COLUMN description_label varchar(40),
ADD COLUMN description_keeps_case boolean NOT NULL DEFAULT false,
ADD CONSTRAINT check_wording_description_label_not_blank CHECK (btrim(description_label) <> '');
