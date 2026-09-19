#!/usr/bin/env bash
# Builds a folder of images and a matching CSV for testing the artwork import and the bulk image
# upload, and salts it with the cases the upload page is meant to report.
#
#   ./generate.sh                          60 images, 4 series, 1200x900
#   ./generate.sh --count 400 --series 8   a bigger run
#   ./generate.sh --size 8000x6000 --format jpg
#                                          every image 48 megapixels, to make processing take real
#                                          time. Big sizes need jpg: a PNG that large is well past
#                                          the 25 MB the upload accepts
#   ./generate.sh --big 6                  leave the run quick but give 6 of the artworks, spread
#                                          through it, a --big-size image. Use this to make a run
#                                          long enough to watch the progress bar and the stop button.
#                                          The big ones are what the run's length comes from, so
#                                          raise this, not --count, to make it longer. Keep
#                                          --big-size under ImageProcessing:MaximumMegapixels (100)
#                                          and its file under the 25 MB upload limit; 8000x6000 is
#                                          48 megapixels and about 14 MB

#   ./generate.sh --clean                  delete what it made
#
# It writes to test-upload-files/ at the repo root unless --out says otherwise, and it clears that
# folder first.
#
# Then, in the app: import <out>/artworks.csv on /admin/catalog/artworks/import to create the
# artworks, and drop <out>/Screenshots on /admin/catalog/artworks/images to give them their images.
set -euo pipefail

# test-upload-files/ at the repo root, which .gitignore covers. Resolved from this script rather
# than the working directory, so it lands in the same place wherever it is run from
OUT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)/test-upload-files"
COUNT=60
SERIES_COUNT=4
SIZE=1200x900
FORMAT=png
BIG_COUNT=0
BIG_SIZE=8000x6000
CLEAN=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --out) OUT="$2"; shift 2 ;;
    --count) COUNT="$2"; shift 2 ;;
    --series) SERIES_COUNT="$2"; shift 2 ;;
    --size) SIZE="$2"; shift 2 ;;
    --format) FORMAT="$2"; shift 2 ;;
    --big) BIG_COUNT="$2"; shift 2 ;;
    --big-size) BIG_SIZE="$2"; shift 2 ;;
    --clean) CLEAN=true; shift ;;
    *) echo "Unknown option: $1" >&2; exit 1 ;;
  esac
done

# --out can name any folder and this deletes it, so refuse one this script didn't make.
# A folder it made always has artworks.csv in it
clear_out() {
  if [[ -e "$OUT" && ! -e "$OUT/artworks.csv" ]]; then
    echo "$OUT exists and wasn't made by this script. Refusing to delete it." >&2
    exit 1
  fi

  rm -rf "$OUT"
}

if [[ "$CLEAN" == true ]]; then
  clear_out
  echo "Removed $OUT"
  exit 0
fi

# ImageMagick 6's "convert"; version 7 renamed it to "magick"
command -v convert >/dev/null || { echo "ImageMagick's convert is not installed." >&2; exit 1; }

# a comma in one of them exercises the CSV quoting, an apostrophe the file name
ALL_SERIES=("Vana'diel Nights" "Gustaberg" "Mines" "Dusk, Dawn" "Ronfaure" "Jeuno" "Bastok" "Windurst")
FIRST_WORDS=(Lantern Bridge Tunnel Ghost Crystal Harbour Moonlit Ancient Silent Broken Distant Frozen Burning Hidden)
LAST_WORDS=(Path Waterfall Descent Chapel Market Ruins Crossing Watchtower Steps Gate Hollow Spire Landing Causeway)

clear_out
ROOT="$OUT/Screenshots"
mkdir -p "$ROOT"

# a plasma fill so no two files are byte-identical, and the title burnt in so a mismatch is visible.
# -depth 8 because ImageMagick writes plasma at 16 bits a channel otherwise, which quadruples the file
draw() {
  local text="$1" path="$2" size="$3"
  convert -size "$size" plasma:fractal -depth 8 \
    -pointsize 48 -fill white -stroke black -strokewidth 2 \
    -annotate +40+80 "$text" "$path"
}


csv_cell() {
  case "$1" in
    *,*|*\"*) printf '"%s"' "${1//\"/\"\"}" ;;
    *) printf '%s' "$1" ;;
  esac
}

CSV="$OUT/artworks.csv"
echo "title,series" > "$CSV"

declare -a NAMES=()
index=0

while [[ ${#NAMES[@]} -lt $COUNT ]]; do
  first="${FIRST_WORDS[$((index % ${#FIRST_WORDS[@]}))]}"
  last="${LAST_WORDS[$(((index / ${#FIRST_WORDS[@]}) % ${#LAST_WORDS[@]}))]}"
  number=$((index / (${#FIRST_WORDS[@]} * ${#LAST_WORDS[@]}) + 1))

  name="$first $last"
  [[ $number -gt 1 ]] && name="$name $number"

  NAMES+=("$name")
  index=$((index + 1))
done

# the big ones are spread through the run rather than bunched at the front, so a slow file lands
# in the middle of the progress bar rather than all of them at the start
BIG_STEP=0
if [[ $BIG_COUNT -gt 0 ]]; then
  [[ $BIG_COUNT -gt $COUNT ]] && BIG_COUNT=$COUNT
  BIG_STEP=$((COUNT / BIG_COUNT))

  # a step that is a multiple of the series count puts every big image in one folder, and the
  # upload reads folder by folder, so they would all arrive together anyway
  [[ $((BIG_STEP % SERIES_COUNT)) -eq 0 ]] && BIG_STEP=$((BIG_STEP + 1))
fi


# each artwork's own extension, since a big one is jpg whatever --format says
declare -a EXTENSIONS=()
BIG_MADE=0

for position in "${!NAMES[@]}"; do
  name="${NAMES[$position]}"
  series="${ALL_SERIES[$((position % SERIES_COUNT))]}"

  size="$SIZE"
  extension="$FORMAT"

  if [[ $BIG_STEP -gt 0 && $BIG_MADE -lt $BIG_COUNT && $((position % BIG_STEP)) -eq 0 ]]; then
    size="$BIG_SIZE"
    # a PNG this large is past the 25 MB the upload accepts
    extension=jpg
    BIG_MADE=$((BIG_MADE + 1))
  fi

  EXTENSIONS+=("$extension")

  mkdir -p "$ROOT/$series"
  draw "$name" "$ROOT/$series/$name.$extension" "$size"

  printf '%s,%s\n' "$(csv_cell "$name")" "$(csv_cell "$series")" >> "$CSV"
done


FIRST_SERIES="${ALL_SERIES[0]}"
SECOND_SERIES="${ALL_SERIES[1]}"

# the same name in two series folders: neither should be attached, both reported
cp "$ROOT/$FIRST_SERIES/${NAMES[0]}.${EXTENSIONS[0]}" "$ROOT/$SECOND_SERIES/${NAMES[0]}.${EXTENSIONS[0]}"


# a folder deeper than the rule allows, so these should be counted and skipped. They have to be
# copied from this series' own folder, which is the only one they are certain to be in
mkdir -p "$ROOT/$FIRST_SERIES/thumbnails"
TOO_DEEP=0
for image in "$ROOT/$FIRST_SERIES"/*; do
  # skips the thumbnails folder itself, and the big images aren't named .$FORMAT
  [[ -f $image ]] || continue
  [[ $TOO_DEEP -lt 3 ]] || break

  cp "$image" "$ROOT/$FIRST_SERIES/thumbnails/"
  TOO_DEEP=$((TOO_DEEP + 1))
done

# not an image, so it should be counted and skipped
echo "Notes about these screenshots." > "$ROOT/$SECOND_SERIES/notes.txt"

# an image loose in the dropped folder rather than a series folder: still shallow enough to count
draw "Harbour At Night" "$ROOT/Harbour At Night.$FORMAT" "$SIZE"

printf '%s,%s\n' "Harbour At Night" "$(csv_cell "$FIRST_SERIES")" >> "$CSV"

# no row in the CSV, so no artwork will have this name
draw "Nothing Matches This" "$ROOT/Nothing Matches This.$FORMAT" "$SIZE"


echo "Wrote $OUT"
echo "  $CSV — $((COUNT + 1)) artworks across $SERIES_COUNT series"
echo "  $ROOT — the folder to drop"
[[ $BIG_MADE -gt 0 ]] && echo "  $BIG_MADE of them are $BIG_SIZE jpg, spread through the run"

echo
echo "Dropping it should count:"
echo "  $((COUNT + 3)) images found"
echo "  $TOO_DEEP files skipped: more than one folder inside the one you dropped"
echo "  1 file skipped: not an image we accept"
echo
echo "and the report should end with:"
echo "  added                                $COUNT"
echo "  named twice in this upload           2 — ${NAMES[0]}, in two series folders"
echo "  no artwork of this type has the name 1 — Nothing Matches This"
