curdir=$(pwd)
for f in $curdir/Rotor.*;
	do
		[ -d $f ] && cd "$f"
		echo ""
		dotnet run
		echo ""
		echo "----------"
		echo ""
		cd ..
	done;